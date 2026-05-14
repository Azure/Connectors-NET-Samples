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
/// <see cref="ServicebusClient"/> from the Azure Connectors SDK.
/// </summary>
public class ServiceBusFunctions
{
    private readonly ILogger<ServiceBusFunctions> _logger;
    private readonly ServicebusClient _serviceBusClient;

    public ServiceBusFunctions(
        ILogger<ServiceBusFunctions> logger,
        ServicebusClient serviceBusClient)
    {
        this._logger = logger;
        this._serviceBusClient = serviceBusClient;
    }

    /// <summary>
    /// Gets the list of queues in the Service Bus namespace.
    /// </summary>
    [Function("ServiceBusGetQueues")]
    public async Task<HttpResponseData> ServiceBusGetQueuesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "servicebus/queues")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("ServiceBusGetQueues: Using generated ServicebusClient from SDK.");

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
            this._logger.LogError(ex, "ServiceBusGetQueues failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in ServiceBusGetQueues.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Gets the list of topics in the Service Bus namespace.
    /// </summary>
    [Function("ServiceBusGetTopics")]
    public async Task<HttpResponseData> ServiceBusGetTopicsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "servicebus/topics")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("ServiceBusGetTopics: Using generated ServicebusClient from SDK.");

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
            this._logger.LogError(ex, "ServiceBusGetTopics failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in ServiceBusGetTopics.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }
}
