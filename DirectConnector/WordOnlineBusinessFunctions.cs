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
    /// Gets the list of sources accessible via the connection.
    /// </summary>
    [Function("WordOnlineBusinessGetSources")]
    public async Task<HttpResponseData> WordOnlineBusinessGetSourcesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "wordonlinebusiness/sources")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("WordOnlineBusinessGetSources: Using generated WordOnlineBusinessClient from SDK.");

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
            this._logger.LogError(ex, "WordOnlineBusinessGetSources failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in WordOnlineBusinessGetSources.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Gets the list of drives accessible via the connection.
    /// </summary>
    [Function("WordOnlineBusinessGetDrives")]
    public async Task<HttpResponseData> WordOnlineBusinessGetDrivesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "wordonlinebusiness/drives")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("WordOnlineBusinessGetDrives: Using generated WordOnlineBusinessClient from SDK.");

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
            this._logger.LogError(ex, "WordOnlineBusinessGetDrives failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in WordOnlineBusinessGetDrives.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }
}
