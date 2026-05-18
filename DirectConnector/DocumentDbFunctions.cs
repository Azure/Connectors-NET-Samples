//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.Documentdb;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating Azure Cosmos DB operations using the generated
/// <see cref="DocumentDbClient"/> from the Azure Connectors SDK.
/// </summary>
/// <remarks>
/// The connector name "documentdb" reflects the original Azure DocumentDB branding;
/// it connects to Azure Cosmos DB accounts.
/// </remarks>
public class DocumentDbFunctions
{
    private readonly ILogger<DocumentDbFunctions> _logger;
    private readonly DocumentDbClient _documentDbClient;

    public DocumentDbFunctions(
        ILogger<DocumentDbFunctions> logger,
        DocumentDbClient documentDbClient)
    {
        this._logger = logger;
        this._documentDbClient = documentDbClient;
    }

    /// <summary>
    /// Lists Cosmos DB accounts available to the connection.
    /// </summary>
    [Function("CosmosDbListAccounts")]
    public async Task<HttpResponseData> CosmosDbListAccountsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "cosmosdb/accounts")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("CosmosDbListAccounts: Using generated DocumentDbClient from SDK.");

        try
        {
            var accounts = await this._documentDbClient
                .GetCosmosDbAccountsAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, accounts }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "CosmosDbListAccounts failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in CosmosDbListAccounts.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Lists databases in a specified Cosmos DB account.
    /// Pass <c>account</c> query parameter with the Cosmos DB account name.
    /// </summary>
    [Function("CosmosDbListDatabases")]
    public async Task<HttpResponseData> CosmosDbListDatabasesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "cosmosdb/databases")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var account = request.Query["account"];

        if (string.IsNullOrEmpty(account))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { success = false, error = "Missing required query parameter 'account'." }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return badRequest;
        }

        this._logger.LogInformation("CosmosDbListDatabases: Listing databases in Cosmos DB account '{Account}'.", account);

        try
        {
            var databases = await this._documentDbClient
                .GetDatabasesAsync(account, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, account, databases }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "CosmosDbListDatabases failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in CosmosDbListDatabases.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }
}
