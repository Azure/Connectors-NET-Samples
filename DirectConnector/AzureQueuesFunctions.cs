//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.Azurequeues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating Azure Storage Queues operations using the generated
/// <see cref="AzureQueuesClient"/> from the Azure Connectors SDK.
/// </summary>
public class AzureQueuesFunctions
{
    private readonly ILogger<AzureQueuesFunctions> _logger;
    private readonly AzureQueuesClient _azureQueuesClient;

    public AzureQueuesFunctions(
        ILogger<AzureQueuesFunctions> logger,
        AzureQueuesClient azureQueuesClient)
    {
        this._logger = logger;
        this._azureQueuesClient = azureQueuesClient;
    }

    /// <summary>
    /// Lists storage accounts available to the Azure Queues connection.
    /// </summary>
    [Function("AzureQueuesListStorageAccounts")]
    public async Task<HttpResponseData> AzureQueuesListStorageAccountsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "azurequeues/storageaccounts")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("AzureQueuesListStorageAccounts: Using generated AzureQueuesClient from SDK.");

        try
        {
            var storageAccounts = await this._azureQueuesClient
                .GetStorageAccountsAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, storageAccounts }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "AzureQueuesListStorageAccounts failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in AzureQueuesListStorageAccounts.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Lists queues in a specified storage account.
    /// Pass <c>storageAccount</c> query parameter with the storage account name or queue endpoint.
    /// </summary>
    [Function("AzureQueuesListQueues")]
    public async Task<HttpResponseData> AzureQueuesListQueuesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "azurequeues/queues")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var storageAccount = request.Query["storageAccount"];

        if (string.IsNullOrWhiteSpace(storageAccount))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { success = false, error = "Missing required query parameter 'storageAccount'." }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return badRequest;
        }

        this._logger.LogInformation("AzureQueuesListQueues: Listing queues in storage account '{StorageAccount}'.", storageAccount);

        try
        {
            var queues = await this._azureQueuesClient
                .ListQueuesAsync(storageAccount, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, storageAccount, queues }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "AzureQueuesListQueues failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in AzureQueuesListQueues.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Gets messages from a queue and demonstrates the nested message response.
    /// </summary>
    [Function("AzureQueuesGetMessages")]
    public async Task<HttpResponseData> AzureQueuesGetMessagesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "azurequeues/messages")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var storageAccount = request.Query["storageAccount"];
        var queueName = request.Query["queueName"];
        if (string.IsNullOrWhiteSpace(storageAccount) || string.IsNullOrWhiteSpace(queueName))
        {
            var response = request.CreateResponse(HttpStatusCode.BadRequest);
            await response
                .WriteAsJsonAsync(new { success = false, error = "Query parameters 'storageAccount' and 'queueName' are required." }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
            return response;
        }

        var numberOfMessages = request.Query["numberOfMessages"];
        var visibilityTimeout = request.Query["visibilityTimeout"];
        try
        {
            var result = await this._azureQueuesClient
                .GetMessagesAsync(
                    storageAccountNameOrQueueEndpoint: storageAccount,
                    queueName: queueName,
                    numberOfMessages: numberOfMessages,
                    visibilityTimeout: visibilityTimeout,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
            var messages = (result.QueueMessagesList?.QueueMessage ?? [])
                .Select(message => new
                {
                    message.MessageId,
                    message.MessageText,
                    message.DequeueCount,
                    popReceipt = message.PopReceipt,
                    nextVisibleTime = message.NextVisibleTime,
                });

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, messages }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "AzureQueuesGetMessages failed with status '{StatusCode}'.", ex.Status);

            var response = request.CreateResponse(HttpStatusCode.BadGateway);
            await response
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
            return response;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in AzureQueuesGetMessages.");

            var response = request.CreateResponse(HttpStatusCode.InternalServerError);
            await response
                .WriteAsJsonAsync(new { success = false, error = ex.Message }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
            return response;
        }
    }

    /// <summary>
    /// Creates a queue in the selected storage account.
    /// </summary>
    [Function("AzureQueuesPutQueue")]
    public Task<HttpResponseData> AzureQueuesPutQueueAsync(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "azurequeues/queues")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var storageAccount = request.Query["storageAccount"];
        var queueName = request.Query["queueName"];
        if (string.IsNullOrWhiteSpace(storageAccount) || string.IsNullOrWhiteSpace(queueName))
        {
            return AzureQueuesFunctions.CreateBadRequestAsync(
                request: request, error: "Query parameters 'storageAccount' and 'queueName' are required.", cancellationToken: cancellationToken);
        }

        return ConnectorFunctionExecutor.ExecuteAsync(
            request: request,
            logger: this._logger,
            operationName: "AzureQueuesPutQueue",
            operation: () => this._azureQueuesClient
                .PutQueueAsync(
                    storageAccountNameOrQueueEndpoint: storageAccount,
                    queueName: queueName,
                    cancellationToken: cancellationToken),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Adds the nonempty, plain-text request body to a queue as a message.
    /// The SDK JSON-encodes this string before sending it to the connector.
    /// </summary>
    [Function("AzureQueuesPutMessage")]
    public async Task<HttpResponseData> AzureQueuesPutMessageAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "azurequeues/messages/send")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var storageAccount = request.Query["storageAccount"];
        var queueName = request.Query["queueName"];
        if (string.IsNullOrWhiteSpace(storageAccount) || string.IsNullOrWhiteSpace(queueName))
        {
            return await AzureQueuesFunctions
                .CreateBadRequestAsync(request, "Query parameters 'storageAccount' and 'queueName' are required.", cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }

        using var reader = new StreamReader(request.Body, leaveOpen: true);
        var input = await reader
            .ReadToEndAsync(cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        if (string.IsNullOrWhiteSpace(input))
        {
            return await AzureQueuesFunctions
                .CreateBadRequestAsync(request, "A nonempty plain-text message body is required.", cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }

        return await ConnectorFunctionExecutor
            .ExecuteAsync(
                request,
                this._logger,
                operationName: "AzureQueuesPutMessage",
                operation: async () =>
                {
                    await this._azureQueuesClient
                        .PutMessageAsync(
                            storageAccountNameOrQueueEndpoint: storageAccount,
                            queueName: queueName,
                            input: input,
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(continueOnCapturedContext: false);
                    return new { storageAccount, queueName };
                },
                cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    /// <summary>
    /// Deletes a message using the message ID and pop receipt returned by GetMessages.
    /// </summary>
    [Function("AzureQueuesDeleteMessage")]
    public Task<HttpResponseData> AzureQueuesDeleteMessageAsync(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "azurequeues/messages")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var storageAccount = request.Query["storageAccount"];
        var queueName = request.Query["queueName"];
        var messageId = request.Query["messageId"];
        var popReceipt = request.Query["popReceipt"];
        if (string.IsNullOrWhiteSpace(storageAccount) || string.IsNullOrWhiteSpace(queueName) ||
            string.IsNullOrWhiteSpace(messageId) || string.IsNullOrWhiteSpace(popReceipt))
        {
            return AzureQueuesFunctions.CreateBadRequestAsync(
                request,
                "Query parameters 'storageAccount', 'queueName', 'messageId', and 'popReceipt' are required.",
                cancellationToken);
        }

        return ConnectorFunctionExecutor.ExecuteAsync(
            request: request,
            logger: this._logger,
            operationName: "AzureQueuesDeleteMessage",
            operation: async () =>
            {
                await this._azureQueuesClient
                    .DeleteMessageAsync(
                        storageAccountNameOrQueueEndpoint: storageAccount,
                        queueName: queueName,
                        messageId: messageId,
                        popReceipt: popReceipt,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
                return new { storageAccount, queueName, messageId };
            },
            cancellationToken: cancellationToken);
    }

    private static async Task<HttpResponseData> CreateBadRequestAsync(
        HttpRequestData request,
        string error,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(HttpStatusCode.BadRequest);
        await response
            .WriteAsJsonAsync(new { success = false, error }, cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        return response;
    }
}
