//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.Eventhubs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating Azure Event Hubs operations using the generated
/// <see cref="EventHubsClient"/> from the Azure Connectors SDK.
/// </summary>
public class EventHubsFunctions
{
    private readonly ILogger<EventHubsFunctions> _logger;
    private readonly EventHubsClient _eventHubsClient;

    public EventHubsFunctions(
        ILogger<EventHubsFunctions> logger,
        EventHubsClient eventHubsClient)
    {
        this._logger = logger;
        this._eventHubsClient = eventHubsClient;
    }

    /// <summary>
    /// Lists Event Hubs available to the connection.
    /// </summary>
    [Function("EventHubsListHubs")]
    public async Task<HttpResponseData> EventHubsListHubsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "eventhubs/hubs")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("EventHubsListHubs: Using generated EventHubsClient from SDK.");

        try
        {
            var eventHubs = await this._eventHubsClient
                .GetEventHubsAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, eventHubs }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "EventHubsListHubs failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in EventHubsListHubs.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Lists supported content types for Event Hubs messages.
    /// </summary>
    [Function("EventHubsListContentTypes")]
    public async Task<HttpResponseData> EventHubsListContentTypesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "eventhubs/contenttypes")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("EventHubsListContentTypes: Listing supported content types.");

        try
        {
            var contentTypes = await this._eventHubsClient
                .GetContentTypesAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, contentTypes }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "EventHubsListContentTypes failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in EventHubsListContentTypes.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }
}
