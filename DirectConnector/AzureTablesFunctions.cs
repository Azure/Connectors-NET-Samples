//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.Azuretables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating Azure Storage Tables operations using the generated
/// <see cref="AzureTablesClient"/> from the Azure Connectors SDK.
/// </summary>
public class AzureTablesFunctions
{
    private readonly ILogger<AzureTablesFunctions> _logger;
    private readonly AzureTablesClient _azureTablesClient;

    public AzureTablesFunctions(
        ILogger<AzureTablesFunctions> logger,
        AzureTablesClient azureTablesClient)
    {
        this._logger = logger;
        this._azureTablesClient = azureTablesClient;
    }

    /// <summary>
    /// Lists storage accounts available to the Azure Tables connection.
    /// </summary>
    [Function("AzureTablesListStorageAccounts")]
    public async Task<HttpResponseData> AzureTablesListStorageAccountsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "azuretables/storageaccounts")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("AzureTablesListStorageAccounts: Using generated AzureTablesClient from SDK.");

        try
        {
            var storageAccounts = await this._azureTablesClient
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
            this._logger.LogError(ex, "AzureTablesListStorageAccounts failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in AzureTablesListStorageAccounts.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Lists tables in a specified storage account.
    /// Pass <c>storageAccount</c> query parameter with the storage account name or table endpoint.
    /// </summary>
    [Function("AzureTablesListTables")]
    public async Task<HttpResponseData> AzureTablesListTablesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "azuretables/tables")] HttpRequestData request,
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

        this._logger.LogInformation("AzureTablesListTables: Listing tables in storage account '{StorageAccount}'.", storageAccount);

        try
        {
            var tables = await this._azureTablesClient
                .GetTablesAsync(storageAccount, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, storageAccount, tables }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "AzureTablesListTables failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in AzureTablesListTables.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }
}
