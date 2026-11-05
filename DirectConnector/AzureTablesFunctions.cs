//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.Azuretables;
using Azure.Connectors.Sdk.Azuretables.Models;
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

    [Function("AzureTablesCreateTable")]
    public Task<HttpResponseData> CreateTableAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "azuretables/tables")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        return this.ExecuteAsync(
            request,
            operationName: "AzureTablesCreateTable",
            operation: () => this._azureTablesClient
                .CreateTableAsync(
                storageAccountNameOrTableEndpoint: request.Query["storageAccount"]!,
                input: request.Query["table"]!,
                cancellationToken: cancellationToken),
            requiredParameters: ["storageAccount", "table"],
            cancellationToken: cancellationToken);
    }

    [Function("AzureTablesGetTable")]
    public Task<HttpResponseData> GetTableAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "azuretables/table")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        return this.ExecuteAsync(
            request: request,
            operationName: "AzureTablesGetTable",
            operation: () => this._azureTablesClient
                .GetTableAsync(
                storageAccountNameOrTableEndpoint: request.Query["storageAccount"]!,
                table: request.Query["table"]!,
                cancellationToken: cancellationToken),
            requiredParameters: ["storageAccount", "table"],
            cancellationToken: cancellationToken);
    }

    [Function("AzureTablesDeleteTable")]
    public Task<HttpResponseData> DeleteTableAsync(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "azuretables/table")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        return this.ExecuteAsync(
            request: request,
            operationName: "AzureTablesDeleteTable",
            operation: async () =>
            {
                await this._azureTablesClient
                    .DeleteTableAsync(
                        storageAccountNameOrTableEndpoint: request.Query["storageAccount"]!,
                        table: request.Query["table"]!,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
                return new { deleted = true };
            },
            requiredParameters: ["storageAccount", "table"],
            cancellationToken: cancellationToken);
    }

    [Function("AzureTablesGetEntities")]
    public Task<HttpResponseData> GetEntitiesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "azuretables/entities")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        return this.ExecuteAsync(
            request: request,
            operationName: "AzureTablesGetEntities",
            operation: () => this._azureTablesClient
                .GetEntitiesAsync(
                storageAccountNameOrTableEndpoint: request.Query["storageAccount"]!,
                table: request.Query["table"]!,
                filterQuery: request.Query["$filter"],
                selectQuery: request.Query["$select"],
                cancellationToken: cancellationToken),
            requiredParameters: ["storageAccount", "table"],
            cancellationToken: cancellationToken);
    }

    [Function("AzureTablesGetEntity")]
    public Task<HttpResponseData> GetEntityAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "azuretables/entity")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        return this.ExecuteAsync(
            request: request,
            operationName: "AzureTablesGetEntity",
            operation: () => this._azureTablesClient
                .GetEntityAsync(
                storageAccountNameOrTableEndpoint: request.Query["storageAccount"]!,
                table: request.Query["table"]!,
                partitionKey: request.Query["partitionKey"]!,
                rowKey: request.Query["rowKey"]!,
                selectQuery: request.Query["$select"],
                cancellationToken: cancellationToken),
            requiredParameters: ["storageAccount", "table", "partitionKey", "rowKey"],
            cancellationToken: cancellationToken);
    }

    [Function("AzureTablesCreateEntity")]
    public Task<HttpResponseData> CreateEntityAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "azuretables/entities")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        return this.ExecuteWriteAsync<CreateEntityInput, InsertEntityResponse>(
            request: request,
            operationName: "AzureTablesCreateEntity",
            operation: async input =>
            {
                return await this._azureTablesClient
                    .CreateEntityAsync(
                        storageAccountNameOrTableEndpoint: request.Query["storageAccount"]!,
                        table: request.Query["table"]!,
                        input: input,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
            },
            requiredParameters: ["storageAccount", "table"],
            requiresBodyKeys: true,
            cancellationToken: cancellationToken);
    }

    [Function("AzureTablesDeleteEntity")]
    public Task<HttpResponseData> DeleteEntityAsync(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "azuretables/entity")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        return this.ExecuteAsync(
            request,
            operationName: "AzureTablesDeleteEntity",
            operation: async () =>
            {
                await this._azureTablesClient
                    .DeleteEntityAsync(
                        storageAccountNameOrTableEndpoint: request.Query["storageAccount"]!,
                        table: request.Query["table"]!,
                        partitionKey: request.Query["partitionKey"]!,
                        rowKey: request.Query["rowKey"]!,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
                return new { deleted = true };
            },
            requiredParameters: ["storageAccount", "table", "partitionKey", "rowKey"],
            cancellationToken: cancellationToken);
    }
    [Function("AzureTablesMergeEntity")]
    public Task<HttpResponseData> MergeEntityAsync(
        [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "azuretables/entity")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        return this.ExecuteWriteAsync<MergeEntityInput, bool>(
            request: request,
            operationName: "AzureTablesMergeEntity",
            operation: async input =>
            {
                await this._azureTablesClient
                    .MergeEntityAsync(
                        storageAccountNameOrTableEndpoint: request.Query["storageAccount"]!,
                        table: request.Query["table"]!,
                        partitionKey: request.Query["partitionKey"]!,
                        rowKey: request.Query["rowKey"]!,
                        input: input,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
                return true;
            },
            requiredParameters: ["storageAccount", "table", "partitionKey", "rowKey"],
            requiresBodyKeys: false,
            cancellationToken: cancellationToken);
    }

    [Function("AzureTablesReplaceEntity")]
    public Task<HttpResponseData> ReplaceEntityAsync(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "azuretables/entity")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        return this.ExecuteWriteAsync<ReplaceEntityInput, bool>(
            request: request,
            operationName: "AzureTablesReplaceEntity",
            operation: async input =>
            {
                await this._azureTablesClient
                    .ReplaceEntityAsync(
                        storageAccountNameOrTableEndpoint: request.Query["storageAccount"]!,
                        table: request.Query["table"]!,
                        partitionKey: request.Query["partitionKey"]!,
                        rowKey: request.Query["rowKey"]!,
                        input: input,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
                return true;
            },
            requiredParameters: ["storageAccount", "table", "partitionKey", "rowKey"],
            requiresBodyKeys: false,
            cancellationToken: cancellationToken);
    }

    [Function("AzureTablesInsertMergeEntity")]
    public Task<HttpResponseData> InsertMergeEntityAsync(
        [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "azuretables/entity/upsert")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        return this.ExecuteWriteAsync<InsertMergeEntityInput, bool>(
            request: request,
            operationName: "AzureTablesInsertMergeEntity",
            operation: async input =>
            {
                await this._azureTablesClient
                    .InsertMergeEntityAsync(
                        storageAccountNameOrTableEndpoint: request.Query["storageAccount"]!,
                        table: request.Query["table"]!,
                        partitionKey: request.Query["partitionKey"]!,
                        rowKey: request.Query["rowKey"]!,
                        input: input,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
                return true;
            },
            requiredParameters: ["storageAccount", "table", "partitionKey", "rowKey"],
            requiresBodyKeys: false,
            cancellationToken: cancellationToken);
    }

    [Function("AzureTablesInsertReplaceEntity")]
    public Task<HttpResponseData> InsertReplaceEntityAsync(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "azuretables/entity/upsert")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        return this.ExecuteWriteAsync<InsertReplaceEntityInput, bool>(
            request: request,
            operationName: "AzureTablesInsertReplaceEntity",
            operation: async input =>
            {
                await this._azureTablesClient
                    .InsertReplaceEntityAsync(
                        storageAccountNameOrTableEndpoint: request.Query["storageAccount"]!,
                        table: request.Query["table"]!,
                        partitionKey: request.Query["partitionKey"]!,
                        rowKey: request.Query["rowKey"]!,
                        input: input,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
                return true;
            },
            requiredParameters: ["storageAccount", "table", "partitionKey", "rowKey"],
            requiresBodyKeys: false,
            cancellationToken: cancellationToken);
    }

    private Task<HttpResponseData> ExecuteAsync<TResult>(
        HttpRequestData request,
        string operationName,
        Func<Task<TResult>> operation,
        string[] requiredParameters,
        CancellationToken cancellationToken)
    {
        var missing = requiredParameters.FirstOrDefault(parameter => string.IsNullOrWhiteSpace(request.Query[parameter]));
        return missing is not null
            ? this.CreateBadRequestAsync(request, $"Missing required query parameter '{missing}'.", cancellationToken)
            : ConnectorFunctionExecutor.ExecuteAsync(request, this._logger, operationName, operation, cancellationToken);
    }

    private async Task<HttpResponseData> ExecuteWriteAsync<TInput, TResult>(
        HttpRequestData request,
        string operationName,
        Func<TInput, Task<TResult>> operation,
        string[] requiredParameters,
        bool requiresBodyKeys,
        CancellationToken cancellationToken)
        where TInput : class
    {
        var missing = requiredParameters.FirstOrDefault(parameter => string.IsNullOrWhiteSpace(request.Query[parameter]));
        if (missing is not null)
        {
            return await this
                .CreateBadRequestAsync(request, $"Missing required query parameter '{missing}'.", cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }

        TInput? input;
        try
        {
            using var document = await JsonDocument
                .ParseAsync(request.Body, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return await this
                    .CreateBadRequestAsync(request, "The entity body must be a JSON object.", cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
            }

            if (requiresBodyKeys && new[] { "PartitionKey", "RowKey" }.Any(key =>
                !document.RootElement.TryGetProperty(key, out var value)
                || value.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(value.GetString())))
            {
                return await this
                    .CreateBadRequestAsync(request, "The entity body requires non-empty 'PartitionKey' and 'RowKey' strings.", cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
            }

            input = document.RootElement.Deserialize<TInput>();
        }
        catch (JsonException ex)
        {
            this._logger.LogWarning("Invalid Azure Tables JSON body ({ExceptionType}).", ex.GetType().Name);
            return await this
                .CreateBadRequestAsync(request, "The entity body is not valid JSON.", cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }

        if (input is null)
        {
            return await this
                .CreateBadRequestAsync(request, "The entity body is required.", cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }

        return await ConnectorFunctionExecutor
            .ExecuteAsync(request, this._logger, operationName, () => operation(input), cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    private async Task<HttpResponseData> CreateBadRequestAsync(
        HttpRequestData request,
        string message,
        CancellationToken cancellationToken)
    {
        this._logger.LogWarning("Azure Tables request rejected: {ValidationMessage}", message);
        var response = request.CreateResponse(HttpStatusCode.BadRequest);
        await response
            .WriteAsJsonAsync(new { success = false, error = message }, cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        return response;
    }
}
