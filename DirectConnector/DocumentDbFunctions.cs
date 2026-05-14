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
/// Azure Functions demonstrating Azure Cosmos DB (DocumentDB) operations using the generated
/// <see cref="DocumentdbClient"/> from the Azure Connectors SDK.
/// </summary>
public class DocumentDbFunctions
{
    private readonly ILogger<DocumentDbFunctions> _logger;
    private readonly DocumentdbClient _documentDbClient;

    public DocumentDbFunctions(
        ILogger<DocumentDbFunctions> logger,
        DocumentdbClient documentDbClient)
    {
        this._logger = logger;
        this._documentDbClient = documentDbClient;
    }

    /// <summary>
    /// Gets the list of Cosmos DB accounts accessible via the connection.
    /// </summary>
    [Function("DocumentDbGetAccounts")]
    public async Task<HttpResponseData> DocumentDbGetAccountsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "documentdb/accounts")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("DocumentDbGetAccounts: Using generated DocumentdbClient from SDK.");

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
            this._logger.LogError(ex, "DocumentDbGetAccounts failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in DocumentDbGetAccounts.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Lists databases in the specified Cosmos DB account.
    /// Route: GET /documentdb/databases?account={azureCosmosDBAccountName}
    /// </summary>
    [Function("DocumentDbGetDatabases")]
    public async Task<HttpResponseData> DocumentDbGetDatabasesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "documentdb/databases")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("DocumentDbGetDatabases: Using generated DocumentdbClient from SDK.");

        var account = System.Web.HttpUtility.ParseQueryString(request.Url.Query)["account"];
        if (string.IsNullOrWhiteSpace(account))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { success = false, error = "Missing required query parameter 'account'." }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return badRequest;
        }

        try
        {
            var databases = await this._documentDbClient
                .GetDatabasesAsync(azureCosmosDBAccountName: account, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, databases }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "DocumentDbGetDatabases failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in DocumentDbGetDatabases.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }
}
