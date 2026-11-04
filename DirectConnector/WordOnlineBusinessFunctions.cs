//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.WordOnlineBusiness;
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
}
