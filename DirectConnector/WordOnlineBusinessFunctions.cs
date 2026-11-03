//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.WordOnlineBusiness;
using Azure.Connectors.Sdk.WordOnlineBusiness.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating Word Online (Business) operations using the generated
/// <see cref="WordOnlineBusinessClient"/> from the Azure Connectors SDK.
/// </summary>
/// <remarks>
/// Word Online Business uses SharePoint-hosted Word files, similar to the Excel Online Business connector.
/// </remarks>
public class WordOnlineBusinessFunctions
{
    private readonly ILogger<WordOnlineBusinessFunctions> _logger;
    private readonly WordOnlineBusinessClient _wordOnlineBusinessClient;

    public WordOnlineBusinessFunctions(
        ILogger<WordOnlineBusinessFunctions> logger,
        WordOnlineBusinessClient wordOnlineBusinessClient)
    {
        this._logger = logger;
        this._wordOnlineBusinessClient = wordOnlineBusinessClient;
    }

    /// <summary>
    /// Lists available SharePoint sources (sites) for the Word Online Business connector.
    /// </summary>
    [Function("WordOnlineGetSources")]
    public async Task<HttpResponseData> WordOnlineGetSourcesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "wordonline/sources")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("WordOnlineGetSources: Using generated WordOnlineBusinessClient from SDK.");

        try
        {
            var sources = await this._wordOnlineBusinessClient
                .GetSourcesAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, sources }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "WordOnlineGetSources failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in WordOnlineGetSources.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Lists available drives (document libraries) for Word Online Business.
    /// </summary>
    [Function("WordOnlineGetDrives")]
    public async Task<HttpResponseData> WordOnlineGetDrivesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "wordonline/drives")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("WordOnlineGetDrives: Listing drives via WordOnlineBusinessClient.");

        try
        {
            var drives = await this._wordOnlineBusinessClient
                .GetDrivesAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, drives }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "WordOnlineGetDrives failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in WordOnlineGetDrives.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    [Function("WordOnlineGetFileSchema")]
    public async Task<HttpResponseData> WordOnlineGetFileSchemaAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "wordonline/templates/schema")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!WordOnlineBusinessFunctions.TryGetFileSelectors(request, out var source, out var drive, out var file))
        {
            return await this
                .BadRequestAsync(
                    request: request,
                    error: "Query parameters 'source', 'drive', and 'file' are required.",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }

        return await ConnectorFunctionExecutor
            .ExecuteAsync(
                request: request,
                logger: this._logger,
                operationName: "WordOnlineGetFileSchema",
                operation: () => this._wordOnlineBusinessClient
                    .GetFileSchemaAsync(
                        source: source!,
                        drive: drive!,
                        file: file!,
                        cancellationToken: cancellationToken),
                cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    [Function("WordOnlinePopulateTemplate")]
    public async Task<HttpResponseData> WordOnlinePopulateTemplateAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "wordonline/templates/populate")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!WordOnlineBusinessFunctions.TryGetFileSelectors(request, out var source, out var drive, out var file))
        {
            return await this
                .BadRequestAsync(
                    request: request,
                    error: "Query parameters 'source', 'drive', and 'file' are required.",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }

        CreateFileItemInput? input;
        try
        {
            using var document = await JsonDocument
                .ParseAsync(utf8Json: request.Body, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return await this
                    .BadRequestAsync(
                        request: request,
                        error: "A JSON object with template fields is required.",
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
            }

            input = document.RootElement.Deserialize<CreateFileItemInput>();
        }
        catch (JsonException)
        {
            return await this
                .BadRequestAsync(
                    request: request,
                    error: "A valid JSON object with template fields is required.",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }

        return await this
            .ExecuteBinaryAsync(
                request: request,
                operationName: "WordOnlinePopulateTemplate",
                contentType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                operation: () => this._wordOnlineBusinessClient
                    .CreateFileItemAsync(
                        input: input!,
                        location: source!,
                        documentLibrary: drive!,
                        file: file!,
                        cancellationToken: cancellationToken),
                cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    [Function("WordOnlineCreateWordFileWithContent")]
    public async Task<HttpResponseData> WordOnlineCreateWordFileWithContentAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "wordonline/files/content")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var fileName = request.Query["fileName"];
        if (fileName is not null && string.IsNullOrWhiteSpace(fileName))
        {
            return await this
                .BadRequestAsync(
                    request: request,
                    error: "Query parameter 'fileName' cannot be blank.",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }

        ContentBody? input;
        try
        {
            input = await JsonSerializer
                .DeserializeAsync<ContentBody>(utf8Json: request.Body, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (JsonException)
        {
            return await this
                .BadRequestAsync(
                    request: request,
                    error: "A valid JSON body with 'content' is required.",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }

        if (string.IsNullOrWhiteSpace(input?.Content))
        {
            return await this
                .BadRequestAsync(
                    request: request,
                    error: "A non-empty 'content' string is required.",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }

        return await ConnectorFunctionExecutor
            .ExecuteAsync(
                request: request,
                logger: this._logger,
                operationName: "WordOnlineCreateWordFileWithContent",
                operation: () => this._wordOnlineBusinessClient
                    .CreateWordFileWithContentAsync(
                        input: input,
                        fileName: fileName,
                        cancellationToken: cancellationToken),
                cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    [Function("WordOnlineGetFilePDF")]
    public async Task<HttpResponseData> WordOnlineGetFilePDFAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "wordonline/files/pdf")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!WordOnlineBusinessFunctions.TryGetFileSelectors(request, out var source, out var drive, out var file))
        {
            return await this
                .BadRequestAsync(
                    request: request,
                    error: "Query parameters 'source', 'drive', and 'file' are required.",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }

        if (!WordOnlineBusinessFunctions.TryParseOptionalBoolean(request.Query["extractSensitivityLabel"], out var extractSensitivityLabel)
            || !WordOnlineBusinessFunctions.TryParseOptionalBoolean(request.Query["sensitivityLabelMetadata"], out var sensitivityLabelMetadata))
        {
            return await this
                .BadRequestAsync(
                    request: request,
                    error: "Query parameters 'extractSensitivityLabel' and 'sensitivityLabelMetadata' must be booleans when supplied.",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }

        return await this
            .ExecuteBinaryAsync(
                request: request,
                operationName: "WordOnlineGetFilePDF",
                contentType: "application/pdf",
                operation: () => this._wordOnlineBusinessClient
                    .GetFilePDFAsync(
                        location: source!,
                        documentLibrary: drive!,
                        file: file!,
                        extractSensitivityLabel: extractSensitivityLabel,
                        sensitivityLabelMetadata: sensitivityLabelMetadata,
                        cancellationToken: cancellationToken),
                cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    private static bool TryGetFileSelectors(HttpRequestData request, out string? source, out string? drive, out string? file)
    {
        source = request.Query["source"];
        drive = request.Query["drive"];
        file = request.Query["file"];
        return !string.IsNullOrWhiteSpace(source) && !string.IsNullOrWhiteSpace(drive) && !string.IsNullOrWhiteSpace(file);
    }

    private static bool TryParseOptionalBoolean(string? value, out bool? parsed)
    {
        parsed = null;
        if (value is null)
        {
            return true;
        }

        if (!bool.TryParse(value, out var booleanValue))
        {
            return false;
        }

        parsed = booleanValue;
        return true;
    }

    private async Task<HttpResponseData> BadRequestAsync(HttpRequestData request, string error, CancellationToken cancellationToken)
    {
        this._logger.LogWarning("Invalid Word Online request: '{Reason}'.", error);
        var response = request.CreateResponse(HttpStatusCode.BadRequest);
        await response
            .WriteAsJsonAsync(new { success = false, error }, cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        return response;
    }

    private async Task<HttpResponseData> ExecuteBinaryAsync(HttpRequestData request, string operationName, string contentType, Func<Task<byte[]>> operation, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await operation()
                .ConfigureAwait(continueOnCapturedContext: false);
            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", contentType);
            await response.Body
                .WriteAsync(bytes, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
            return response;
        }
        catch (ConnectorException ex)
        {
            return await this
                .ConnectorErrorAsync(
                    request: request,
                    operationName: operationName,
                    ex: ex,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    private async Task<HttpResponseData> ConnectorErrorAsync(HttpRequestData request, string operationName, ConnectorException ex, CancellationToken cancellationToken)
    {
        this._logger.LogError(ex, "Operation '{OperationName}' failed with status '{StatusCode}'.", operationName, ex.Status);
        var response = request.CreateResponse(HttpStatusCode.BadGateway);
        await response
            .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        return response;
    }
}
