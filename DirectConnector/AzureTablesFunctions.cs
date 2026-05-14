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
/// Azure Functions demonstrating Azure Tables operations using the generated
/// <see cref="AzuretablesClient"/> from the Azure Connectors SDK.
/// </summary>
public class AzureTablesFunctions
{
    private readonly ILogger<AzureTablesFunctions> _logger;
    private readonly AzuretablesClient _azureTablesClient;

    public AzureTablesFunctions(
        ILogger<AzureTablesFunctions> logger,
        AzuretablesClient azureTablesClient)
    {
        this._logger = logger;
        this._azureTablesClient = azureTablesClient;
    }

    /// <summary>
    /// Gets the list of Azure Storage accounts accessible via the connection.
    /// </summary>
    [Function("AzureTablesGetStorageAccounts")]
    public async Task<HttpResponseData> AzureTablesGetStorageAccountsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "azuretables/accounts")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("AzureTablesGetStorageAccounts: Using generated AzuretablesClient from SDK.");

        try
        {
            var accounts = await this._azureTablesClient
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
            this._logger.LogError(ex, "AzureTablesGetStorageAccounts failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in AzureTablesGetStorageAccounts.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Lists tables in the specified Azure Storage account.
    /// Route: GET /azuretables/tables?storageAccount={storageAccountNameOrTableEndpoint}
    /// </summary>
    [Function("AzureTablesGetTables")]
    public async Task<HttpResponseData> AzureTablesGetTablesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "azuretables/tables")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("AzureTablesGetTables: Using generated AzuretablesClient from SDK.");

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
            var tables = await this._azureTablesClient
                .GetTablesAsync(storageAccountNameOrTableEndpoint: storageAccount, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, tables }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "AzureTablesGetTables failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in AzureTablesGetTables.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }
}
