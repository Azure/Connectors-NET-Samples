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
public class DataverseFunctions
{
    private const int MaxTriggerCallbackBodySize = 1 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly CommondataserviceClient _dataverseClient;
    private readonly ILogger<DataverseFunctions> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataverseFunctions"/> class.
    /// </summary>
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

        var topCountValue = request.Query["$top"];
        int? topCount = null;
        if (!string.IsNullOrWhiteSpace(topCountValue))
        {
            if (!int.TryParse(topCountValue, out var parsedTopCount) || parsedTopCount <= 0)
            {
                return DataverseFunctions.CreateBadRequestAsync(
                    request,
                    message: "Query parameter '$top' must be a positive integer.",
                    cancellationToken: cancellationToken);
            }

            topCount = parsedTopCount;
        }

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
    /// Follows a Dataverse connector next-link value to retrieve a subsequent page.
    /// </summary>
    [Function("DataverseGetNextPage")]
    public Task<HttpResponseData> GetNextPageAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "dataverse/nextpage")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var nextLink = request.Query["nextLink"];
        if (string.IsNullOrWhiteSpace(nextLink))
        {
            return DataverseFunctions.CreateBadRequestAsync(
                request,
                message: "Query parameter 'nextLink' is required.",
                cancellationToken: cancellationToken);
        }

        return this.ExecuteAsync(
            request,
            operationName: "DataverseGetNextPage",
            operation: async () => await this._dataverseClient
                .GetNextPageAsync(nextLinkValue: nextLink, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Receives a typed callback when a Dataverse row is added.
    /// </summary>
    [Function("DataverseOnNewItems")]
    [ConnectorTriggerMetadata(
        ConnectorName = ConnectorNames.MicrosoftDataverse,
        OperationName = CommondataserviceTriggerOperations.OnNewItems,
        Connection = "Connectors:Dataverse")]
    public async Task<HttpResponseData> OnNewItemsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "dataverse/trigger/newitems")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("DataverseOnNewItems: Received Connector Gateway trigger callback.");

        try
        {
            var payload = await ConnectorTriggerPayload
                .ReadAsync<CommondataserviceOnNewItemsTriggerPayload>(
                    request.Body,
                    maxBodySizeBytes: DataverseFunctions.MaxTriggerCallbackBodySize,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var itemCount = payload?.Body?.Value?.Count ?? 0;

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    receivedAt = DateTime.UtcNow,
                    itemCount,
                    triggerPayloadReader = "ConnectorTriggerPayload",
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (JsonException ex)
        {
            this._logger.LogError(ex, "DataverseOnNewItems: Invalid JSON payload.");
        }
        catch (InvalidOperationException ex)
        {
            this._logger.LogWarning(ex, "DataverseOnNewItems: Payload exceeded the configured limit.");
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in DataverseOnNewItems.");
        }

        var acknowledgment = request.CreateResponse(HttpStatusCode.OK);
        await acknowledgment
            .WriteAsJsonAsync(new
            {
                success = true,
                receivedAt = DateTime.UtcNow,
                itemCount = 0,
                triggerPayloadReader = "ConnectorTriggerPayload",
            })
            .ConfigureAwait(continueOnCapturedContext: false);

        return acknowledgment;
    }

    /// <summary>
    /// Gets a Dataverse record by identifier.
    /// </summary>
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
    /// Creates a note attachment for a Dataverse record from the request body bytes.
    /// </summary>
    [Function("DataverseCreateAttachment")]
    public async Task<HttpResponseData> CreateAttachmentAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "dataverse/items/{itemIdentifier}/attachments")] HttpRequestData request,
        string itemIdentifier,
        CancellationToken cancellationToken)
    {
        var environment = request.Query["environment"];
        var tableName = request.Query["tableName"];
        var fileName = request.Query["fileName"];
        if (string.IsNullOrWhiteSpace(environment) ||
            string.IsNullOrWhiteSpace(tableName) ||
            string.IsNullOrWhiteSpace(fileName))
        {
            return await DataverseFunctions.CreateBadRequestAsync(
                request,
                message: "Query parameters 'environment', 'tableName', and 'fileName' are required.",
                cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }

        using var content = new MemoryStream();
        await request.Body
            .CopyToAsync(content, cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return await this.ExecuteAsync(
            request,
            operationName: "DataverseCreateAttachment",
            operation: async () => await this._dataverseClient
                .CreateAttachmentAsync(
                    dataset: environment,
                    table: tableName,
                    id: itemIdentifier,
                    input: content.ToArray(),
                    fileName: fileName,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false),
            cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
    }

    /// <summary>
    /// Deletes a Dataverse record by identifier.
    /// </summary>
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
            await response
                .WriteAsJsonAsync(result, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
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
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(request.Body, DataverseFunctions.JsonOptions, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (JsonException ex)
        {
            this._logger.LogWarning(ex, "Unable to deserialize the Dataverse request body.");
            return default;
        }
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
