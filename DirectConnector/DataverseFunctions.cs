//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.Commondataservice;
using Azure.Connectors.Sdk.Commondataservice.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating Microsoft Dataverse discovery and record CRUD operations.
/// </summary>
/// <remarks>
/// NOTE(daviburg): The endpoints form an end-to-end flow: discover an environment and table, then create, read, update, and delete a record.
/// </remarks>
public class DataverseFunctions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly CommondataserviceClient _dataverseClient;
    private readonly ILogger<DataverseFunctions> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataverseFunctions"/> class.
    /// </summary>
    /// <remarks>
    /// NOTE(daviburg): The host owns the singleton client's lifetime through dependency injection.
    /// </remarks>
    public DataverseFunctions(
        ILogger<DataverseFunctions> logger,
        CommondataserviceClient dataverseClient)
    {
        this._logger = logger;
        this._dataverseClient = dataverseClient;
    }

    /// <summary>
    /// Lists Dataverse environments that are available to the configured connection.
    /// </summary>
    /// <remarks>
    /// NOTE(daviburg): Use each returned environment URL as the environment route parameter for the remaining endpoints.
    /// </remarks>
    [Function("DataverseListEnvironments")]
    public Task<HttpResponseData> ListEnvironmentsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "dataverse/environments")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        return this.ExecuteAsync(
            request,
            operationName: "DataverseListEnvironments",
            operation: async () => await this._dataverseClient
                .GetDataSetsAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Lists tables in a Dataverse environment.
    /// </summary>
    /// <remarks>
    /// NOTE(daviburg): Discover valid environment values with <see cref="ListEnvironmentsAsync(HttpRequestData, CancellationToken)"/>.
    /// </remarks>
    [Function("DataverseListTables")]
    public Task<HttpResponseData> ListTablesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "dataverse/tables")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var environment = request.Query["environment"];
        if (string.IsNullOrWhiteSpace(environment))
        {
            return DataverseFunctions.CreateBadRequestAsync(
                request,
                message: "Query parameter 'environment' is required.",
                cancellationToken: cancellationToken);
        }

        return this.ExecuteAsync(
            request,
            operationName: "DataverseListTables",
            operation: async () => await this._dataverseClient
                .GetTablesAsync(dataset: environment, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Lists records from a Dataverse table.
    /// </summary>
    /// <remarks>
    /// NOTE(daviburg): The optional OData filter and ordering query parameters are forwarded to the generated client.
    /// </remarks>
    [Function("DataverseListItems")]
    public Task<HttpResponseData> ListItemsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "dataverse/items")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var environment = request.Query["environment"];
        var tableName = request.Query["tableName"];
        if (string.IsNullOrWhiteSpace(environment) || string.IsNullOrWhiteSpace(tableName))
        {
            return DataverseFunctions.CreateBadRequestAsync(
                request,
                message: "Query parameters 'environment' and 'tableName' are required.",
                cancellationToken: cancellationToken);
        }

        int? topCount = int.TryParse(request.Query["$top"], out var parsedTopCount)
            ? parsedTopCount
            : null;

        return this.ExecuteAsync(
            request,
            operationName: "DataverseListItems",
            operation: async () => await this._dataverseClient
                .GetItemsAsync(
                    environment: environment,
                    tableName: tableName,
                    filterQuery: request.Query["$filter"],
                    orderBy: request.Query["$orderby"],
                    topCount: topCount,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Gets a Dataverse record by identifier.
    /// </summary>
    /// <remarks>
    /// NOTE(daviburg): Record properties are dynamic because each Dataverse table has its own schema.
    /// </remarks>
    [Function("DataverseGetItem")]
    public Task<HttpResponseData> GetItemAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "dataverse/items/{itemIdentifier}")] HttpRequestData request,
        string itemIdentifier,
        CancellationToken cancellationToken)
    {
        var environment = request.Query["environment"];
        var tableName = request.Query["tableName"];
        if (string.IsNullOrWhiteSpace(environment) || string.IsNullOrWhiteSpace(tableName))
        {
            return DataverseFunctions.CreateBadRequestAsync(
                request,
                message: "Query parameters 'environment' and 'tableName' are required.",
                cancellationToken: cancellationToken);
        }

        return this.ExecuteAsync(
            request,
            operationName: "DataverseGetItem",
            operation: async () => await this._dataverseClient
                .GetItemAsync(
                    environment: environment,
                    tableName: tableName,
                    itemIdentifier: itemIdentifier,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates a Dataverse record from a JSON request body.
    /// </summary>
    /// <remarks>
    /// NOTE(daviburg): JSON fields are preserved as dynamic properties so the same endpoint supports any writable table.
    /// </remarks>
    [Function("DataverseCreateItem")]
    public async Task<HttpResponseData> CreateItemAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "dataverse/items")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var environment = request.Query["environment"];
        var tableName = request.Query["tableName"];
        if (string.IsNullOrWhiteSpace(environment) || string.IsNullOrWhiteSpace(tableName))
        {
            return await DataverseFunctions.CreateBadRequestAsync(
                request,
                message: "Query parameters 'environment' and 'tableName' are required.",
                cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }

        var input = await this.ReadBodyAsync<PostItemInput>(request, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        if (input == null)
        {
            return await DataverseFunctions.CreateBadRequestAsync(
                request,
                message: "Request body must contain a valid JSON object.",
                cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }

        return await this.ExecuteAsync(
            request,
            operationName: "DataverseCreateItem",
            operation: async () => await this._dataverseClient
                .PostItemAsync(
                    environment: environment,
                    tableName: tableName,
                    input: input,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false),
            cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }

    /// <summary>
    /// Updates a Dataverse record from a JSON request body.
    /// </summary>
    /// <remarks>
    /// NOTE(daviburg): JSON fields are preserved as dynamic properties so callers update only the fields they supply.
    /// </remarks>
    [Function("DataverseUpdateItem")]
    public async Task<HttpResponseData> UpdateItemAsync(
        [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "dataverse/items/{itemIdentifier}")] HttpRequestData request,
        string itemIdentifier,
        CancellationToken cancellationToken)
    {
        var environment = request.Query["environment"];
        var tableName = request.Query["tableName"];
        if (string.IsNullOrWhiteSpace(environment) || string.IsNullOrWhiteSpace(tableName))
        {
            return await DataverseFunctions.CreateBadRequestAsync(
                request,
                message: "Query parameters 'environment' and 'tableName' are required.",
                cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }

        var input = await this.ReadBodyAsync<PatchItemInput>(request, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        if (input == null)
        {
            return await DataverseFunctions.CreateBadRequestAsync(
                request,
                message: "Request body must contain a valid JSON object.",
                cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }

        return await this.ExecuteAsync(
            request,
            operationName: "DataverseUpdateItem",
            operation: async () => await this._dataverseClient
                .PatchItemAsync(
                    environment: environment,
                    tableName: tableName,
                    rowIdentifier: itemIdentifier,
                    input: input,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false),
            cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }

    /// <summary>
    /// Deletes a Dataverse record by identifier.
    /// </summary>
    /// <remarks>
    /// NOTE(daviburg): This endpoint completes only when the connector confirms the record was deleted.
    /// </remarks>
    [Function("DataverseDeleteItem")]
    public async Task<HttpResponseData> DeleteItemAsync(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "dataverse/items/{itemIdentifier}")] HttpRequestData request,
        string itemIdentifier,
        CancellationToken cancellationToken)
    {
        var environment = request.Query["environment"];
        var tableName = request.Query["tableName"];
        if (string.IsNullOrWhiteSpace(environment) || string.IsNullOrWhiteSpace(tableName))
        {
            return await DataverseFunctions.CreateBadRequestAsync(
                request,
                message: "Query parameters 'environment' and 'tableName' are required.",
                cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }

        try
        {
            await this._dataverseClient
                .DeleteItemAsync(
                    environment: environment,
                    tableName: tableName,
                    itemIdentifier: itemIdentifier,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return request.CreateResponse(HttpStatusCode.NoContent);
        }
        catch (ConnectorException ex)
        {
            return await this.CreateConnectorErrorResponseAsync(request, "DataverseDeleteItem", ex, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            return await this.CreateUnexpectedErrorResponseAsync(request, "DataverseDeleteItem", ex, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    private async Task<HttpResponseData> ExecuteAsync<T>(
        HttpRequestData request,
        string operationName,
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await operation().ConfigureAwait(continueOnCapturedContext: false);
            var response = request.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            return response;
        }
        catch (ConnectorException ex)
        {
            return await this.CreateConnectorErrorResponseAsync(request, operationName, ex, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            return await this.CreateUnexpectedErrorResponseAsync(request, operationName, ex, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    private async Task<T?> ReadBodyAsync<T>(HttpRequestData request, CancellationToken cancellationToken)
    {
        return await JsonSerializer.DeserializeAsync<T>(request.Body, DataverseFunctions.JsonOptions, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }

    private static async Task<HttpResponseData> CreateBadRequestAsync(
        HttpRequestData request,
        string message,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(HttpStatusCode.BadRequest);
        await response.WriteAsJsonAsync(new { error = message }, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        return response;
    }

    private async Task<HttpResponseData> CreateConnectorErrorResponseAsync(
        HttpRequestData request,
        string operationName,
        ConnectorException exception,
        CancellationToken cancellationToken)
    {
        this._logger.LogError(exception, "{OperationName} failed with status '{StatusCode}'.", operationName, exception.Status);

        var response = request.CreateResponse(HttpStatusCode.BadGateway);
        await response.WriteAsJsonAsync(
            new { success = false, error = exception.Message, statusCode = exception.Status, details = exception.ResponseBody },
            cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        return response;
    }

    private async Task<HttpResponseData> CreateUnexpectedErrorResponseAsync(
        HttpRequestData request,
        string operationName,
        Exception exception,
        CancellationToken cancellationToken)
    {
        this._logger.LogError(exception, "Unexpected error in '{OperationName}'.", operationName);

        var response = request.CreateResponse(HttpStatusCode.InternalServerError);
        await response.WriteAsJsonAsync(new { success = false, error = exception.Message }, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        return response;
    }
}
