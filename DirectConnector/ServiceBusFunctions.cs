//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.Servicebus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating Azure Service Bus operations using the generated
/// <see cref="ServiceBusConnectorClient"/> from the Azure Connectors SDK.
/// </summary>
public class ServiceBusFunctions
{
    private readonly ILogger<ServiceBusFunctions> _logger;
    private readonly ServiceBusConnectorClient _serviceBusClient;

    public ServiceBusFunctions(
        ILogger<ServiceBusFunctions> logger,
        ServiceBusConnectorClient serviceBusClient)
    {
        this._logger = logger;
        this._serviceBusClient = serviceBusClient;
    }

    /// <summary>
    /// Lists Service Bus queues available to the connection.
    /// </summary>
    [Function("ServiceBusListQueues")]
    public async Task<HttpResponseData> ServiceBusListQueuesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "servicebus/queues")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("ServiceBusListQueues: Using generated ServiceBusConnectorClient from SDK.");

        try
        {
            var queues = await this._serviceBusClient
                .GetQueuesAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, queues }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "ServiceBusListQueues failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in ServiceBusListQueues.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Lists Service Bus topics available to the connection.
    /// </summary>
    [Function("ServiceBusListTopics")]
    public async Task<HttpResponseData> ServiceBusListTopicsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "servicebus/topics")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("ServiceBusListTopics: Listing topics via ServiceBusConnectorClient.");

        try
        {
            var topics = await this._serviceBusClient
                .GetTopicsAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, topics }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "ServiceBusListTopics failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in ServiceBusListTopics.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Lists Service Bus entities (queues and topics) available to the connection.
    /// </summary>
    [Function("ServiceBusListEntities")]
    public async Task<HttpResponseData> ServiceBusListEntitiesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "servicebus/entities")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("ServiceBusListEntities: Listing all entities via ServiceBusConnectorClient.");

        try
        {
            var entities = await this._serviceBusClient
                .GetEntitiesAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, entities }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "ServiceBusListEntities failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in ServiceBusListEntities.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }
}
