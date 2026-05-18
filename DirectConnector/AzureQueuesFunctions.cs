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

        if (string.IsNullOrEmpty(storageAccount))
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
}
