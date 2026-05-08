//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Azure.Connectors.Sdk.Mq;
using Azure.Connectors.Sdk.Mq.Models;
using Azure.Connectors.Sdk;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating IBM MQ operations using the generated
/// <see cref="MqClient"/> from the DirectClient SDK.
/// </summary>
/// <remarks>
/// IBM MQ uses parameter-based auth (server, queue manager, channel, credentials).
/// The connection must be created with parameterValues — no OAuth consent flow.
/// </remarks>
public class MqFunctions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<MqFunctions> _logger;
    private readonly MqClient _mqClient;

    public MqFunctions(
        ILogger<MqFunctions> logger,
        MqClient mqClient)
    {
        this._logger = logger;
        this._mqClient = mqClient;
    }

    /// <summary>
    /// Sends a message to an IBM MQ queue.
    /// </summary>
    [Function("MqSendMessage")]
    public async Task<HttpResponseData> MqSendMessageAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "mq/send")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("MqSendMessage: Using generated MqClient from SDK.");

        try
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader
                .ReadToEndAsync(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var input = JsonSerializer.Deserialize<MqSendRequest>(body, MqFunctions.JsonOptions);

            if (input == null || string.IsNullOrEmpty(input.Message))
            {
                var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest
                    .WriteAsJsonAsync(new { error = "Request body must contain 'message'. Optional: 'queue'." })
                    .ConfigureAwait(continueOnCapturedContext: false);

                return badRequest;
            }

            var result = await this._mqClient
                .SendAsync(
                    new SendValidDataOptions { Message = input.Message, Queue = input.Queue },
                    cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    messageId = result.MessageId,
                    correlationId = result.CorrelationId
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "MQ send failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (JsonException ex)
        {
            this._logger.LogWarning(ex, "Invalid JSON in request body.");

            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { error = "Request body must contain valid JSON." })
                .ConfigureAwait(continueOnCapturedContext: false);

            return badRequest;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error in MqSendMessage.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Browses (peeks) a single message from an IBM MQ queue without removing it.
    /// </summary>
    [Function("MqBrowseMessage")]
    public async Task<HttpResponseData> MqBrowseMessageAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "mq/browse")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("MqBrowseMessage: Browse message from MQ queue.");

        try
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader
                .ReadToEndAsync(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var input = JsonSerializer.Deserialize<MqQueueRequest>(body, MqFunctions.JsonOptions);

            var result = await this._mqClient
                .ReadAsync(
                    new SingleGetValidOptions { Queue = input?.Queue },
                    cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(result)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "MQ browse failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (JsonException ex)
        {
            this._logger.LogWarning(ex, "Invalid JSON in request body.");

            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { error = "Request body must contain valid JSON." })
                .ConfigureAwait(continueOnCapturedContext: false);

            return badRequest;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error in MqBrowseMessage.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Browses multiple messages from an IBM MQ queue without removing them.
    /// </summary>
    [Function("MqBrowseMessages")]
    public async Task<HttpResponseData> MqBrowseMessagesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "mq/browse/batch")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("MqBrowseMessages: Browse multiple messages from MQ queue.");

        try
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader
                .ReadToEndAsync(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var input = JsonSerializer.Deserialize<MqBatchRequest>(body, MqFunctions.JsonOptions);

            var result = await this._mqClient
                .ReadAllAsync(
                    new MultipleGetValidOptions
                    {
                        Queue = input?.Queue,
                        BatchSize = input?.BatchSize ?? 10
                    },
                    cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(result)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "MQ browse batch failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (JsonException ex)
        {
            this._logger.LogWarning(ex, "Invalid JSON in request body.");

            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { error = "Request body must contain valid JSON." })
                .ConfigureAwait(continueOnCapturedContext: false);

            return badRequest;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error in MqBrowseMessages.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Receives (destructive get) a single message from an IBM MQ queue.
    /// </summary>
    [Function("MqReceiveMessage")]
    public async Task<HttpResponseData> MqReceiveMessageAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "mq/receive")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("MqReceiveMessage: Destructive get from MQ queue.");

        try
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader
                .ReadToEndAsync(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var input = JsonSerializer.Deserialize<MqQueueRequest>(body, MqFunctions.JsonOptions);

            var result = await this._mqClient
                .ReceiveAsync(
                    new SingleGetValidOptions { Queue = input?.Queue },
                    cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(result)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "MQ receive failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (JsonException ex)
        {
            this._logger.LogWarning(ex, "Invalid JSON in request body.");

            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { error = "Request body must contain valid JSON." })
                .ConfigureAwait(continueOnCapturedContext: false);

            return badRequest;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error in MqReceiveMessage.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Deletes a single message from an IBM MQ queue.
    /// </summary>
    [Function("MqDeleteMessage")]
    public async Task<HttpResponseData> MqDeleteMessageAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "mq/delete")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("MqDeleteMessage: Delete message from MQ queue.");

        try
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader
                .ReadToEndAsync(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var input = JsonSerializer.Deserialize<MqQueueRequest>(body, MqFunctions.JsonOptions);

            var result = await this._mqClient
                .DeleteAsync(
                    new SingleGetValidOptions { Queue = input?.Queue },
                    cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    deleted = true,
                    messageId = result.MessageId
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "MQ delete failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (JsonException ex)
        {
            this._logger.LogWarning(ex, "Invalid JSON in request body.");

            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { error = "Request body must contain valid JSON." })
                .ConfigureAwait(continueOnCapturedContext: false);

            return badRequest;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error in MqDeleteMessage.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    private record MqSendRequest(string? Message, string? Queue);
    private record MqQueueRequest(string? Queue);
    private record MqBatchRequest(string? Queue, double? BatchSize);
}
