//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.AzureEventGrid;
using Azure.Connectors.Sdk.AzureEventGrid.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating Azure Event Grid operations using the generated
/// <see cref="AzureEventGridClient"/> from the Azure Connectors SDK.
/// </summary>
public class AzureEventGridFunctions
{
    private readonly ILogger<AzureEventGridFunctions> _logger;
    private readonly AzureEventGridClient _eventGridClient;

    public AzureEventGridFunctions(
        ILogger<AzureEventGridFunctions> logger,
        AzureEventGridClient eventGridClient)
    {
        this._logger = logger;
        this._eventGridClient = eventGridClient;
    }

    /// <summary>
    /// Lists available Event Grid topic types.
    /// </summary>
    [Function("EventGridListTopicTypes")]
    public async Task<HttpResponseData> EventGridListTopicTypesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "eventgrid/topictypes")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("EventGridListTopicTypes: Using generated AzureEventGridClient from SDK.");

        try
        {
            var topicTypes = await this._eventGridClient
                .TopicTypesListAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, topicTypes }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "EventGridListTopicTypes failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in EventGridListTopicTypes.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Lists Event Grid subscriptions.
    /// </summary>
    [Function("EventGridListSubscriptions")]
    public async Task<HttpResponseData> EventGridListSubscriptionsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "eventgrid/subscriptions")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("EventGridListSubscriptions: Using generated AzureEventGridClient from SDK.");

        try
        {
            var subscriptions = await this._eventGridClient
                .SubscriptionsListAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, subscriptions }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "EventGridListSubscriptions failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in EventGridListSubscriptions.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }
}
