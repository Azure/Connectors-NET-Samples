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
/// Azure Functions demonstrating Azure Queues operations using the generated
/// <see cref="AzurequeuesClient"/> from the Azure Connectors SDK.
/// </summary>
public class AzureQueuesFunctions
{
    private readonly ILogger<AzureQueuesFunctions> _logger;
    private readonly AzurequeuesClient _azureQueuesClient;

    public AzureQueuesFunctions(
        ILogger<AzureQueuesFunctions> logger,
        AzurequeuesClient azureQueuesClient)
    {
        this._logger = logger;
        this._azureQueuesClient = azureQueuesClient;
    }

    /// <summary>
    /// Gets the list of Azure Storage accounts accessible via the connection.
    /// </summary>
    [Function("AzureQueuesGetStorageAccounts")]
    public async Task<HttpResponseData> AzureQueuesGetStorageAccountsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "azurequeues/accounts")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("AzureQueuesGetStorageAccounts: Using generated AzurequeuesClient from SDK.");

        try
        {
            var accounts = await this._azureQueuesClient
                .GetStorageAccountsAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, accounts }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "AzureQueuesGetStorageAccounts failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in AzureQueuesGetStorageAccounts.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Lists queues in the specified Azure Storage account.
    /// Route: GET /azurequeues/queues?storageAccount={storageAccountNameOrQueueEndpoint}
    /// </summary>
    [Function("AzureQueuesListQueues")]
    public async Task<HttpResponseData> AzureQueuesListQueuesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "azurequeues/queues")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("AzureQueuesListQueues: Using generated AzurequeuesClient from SDK.");

        var storageAccount = System.Web.HttpUtility.ParseQueryString(request.Url.Query)["storageAccount"];
        if (string.IsNullOrWhiteSpace(storageAccount))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { success = false, error = "Missing required query parameter 'storageAccount'." }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return badRequest;
        }

        try
        {
            var queues = await this._azureQueuesClient
                .ListQueuesAsync(storageAccountNameOrQueueEndpoint: storageAccount, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, queues }, cancellationToken)
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
