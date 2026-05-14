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
/// <see cref="EventhubsClient"/> from the Azure Connectors SDK.
/// </summary>
public class EventHubsFunctions
{
    private readonly ILogger<EventHubsFunctions> _logger;
    private readonly EventhubsClient _eventHubsClient;

    public EventHubsFunctions(
        ILogger<EventHubsFunctions> logger,
        EventhubsClient eventHubsClient)
    {
        this._logger = logger;
        this._eventHubsClient = eventHubsClient;
    }

    /// <summary>
    /// Gets the list of Event Hubs accessible via the connection.
    /// </summary>
    [Function("EventHubsGetEventHubs")]
    public async Task<HttpResponseData> EventHubsGetEventHubsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "eventhubs/list")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("EventHubsGetEventHubs: Using generated EventhubsClient from SDK.");

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
            this._logger.LogError(ex, "EventHubsGetEventHubs failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in EventHubsGetEventHubs.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Gets the consumer groups for the specified Event Hub.
    /// Route: GET /eventhubs/consumergroups?eventHub={eventHubName}
    /// </summary>
    [Function("EventHubsGetConsumerGroups")]
    public async Task<HttpResponseData> EventHubsGetConsumerGroupsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "eventhubs/consumergroups")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("EventHubsGetConsumerGroups: Using generated EventhubsClient from SDK.");

        var eventHubName = System.Web.HttpUtility.ParseQueryString(request.Url.Query)["eventHub"];
        if (string.IsNullOrWhiteSpace(eventHubName))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { success = false, error = "Missing required query parameter 'eventHub'." }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return badRequest;
        }

        try
        {
            var consumerGroups = await this._eventHubsClient
                .GetConsumerGroupsAsync(theEventHubName: eventHubName, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, consumerGroups }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "EventHubsGetConsumerGroups failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in EventHubsGetConsumerGroups.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }
}
