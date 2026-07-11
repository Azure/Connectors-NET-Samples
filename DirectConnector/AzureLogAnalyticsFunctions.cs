//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.AzureMonitorLogs;
using Azure.Connectors.Sdk.AzureMonitorLogs.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating Azure Monitor Logs operations using the generated
/// <see cref="AzureMonitorLogsClient"/> from the Azure Connectors SDK.
/// </summary>
/// <remarks>
/// This connector replaces the deprecated Azure Log Analytics connector.
/// The file retains the "LogAnalytics" name in routes for backward compatibility.
/// </remarks>
public class AzureLogAnalyticsFunctions
{
    private const string OperationalInsightsWorkspaceResourceType = "Microsoft.OperationalInsights/workspaces";

    private readonly ILogger<AzureLogAnalyticsFunctions> _logger;
    private readonly AzureMonitorLogsClient _logAnalyticsClient;

    public AzureLogAnalyticsFunctions(
        ILogger<AzureLogAnalyticsFunctions> logger,
        AzureMonitorLogsClient logAnalyticsClient)
    {
        this._logger = logger;
        this._logAnalyticsClient = logAnalyticsClient;
    }

    /// <summary>
    /// Lists Azure subscriptions available to the authenticated connection.
    /// Demonstrates paginated enumeration via <c>await foreach</c> over <c>ConnectorPageable</c>.
    /// </summary>
    [Function("ListLogAnalyticsSubscriptions")]
    public async Task<HttpResponseData> ListSubscriptionsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "loganalytics/subscriptions")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("ListLogAnalyticsSubscriptions: Using generated AzureMonitorLogsClient from SDK.");

        try
        {
            var subscriptions = new List<Subscription>();
            await foreach (var subscription in this._logAnalyticsClient
                .ListSubscriptionsAsync()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false))
            {
                subscriptions.Add(subscription);
            }

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, count = subscriptions.Count, subscriptions }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "ListLogAnalyticsSubscriptions failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in ListLogAnalyticsSubscriptions.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Lists Log Analytics workspaces in a subscription and resource group.
    /// Demonstrates paginated enumeration with query parameters passed to the connector.
    /// </summary>
    [Function("ListLogAnalyticsWorkspaces")]
    public async Task<HttpResponseData> ListWorkspacesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "loganalytics/workspaces")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("ListLogAnalyticsWorkspaces: Using generated AzureMonitorLogsClient from SDK.");

        var subscription = request.Query["subscription"];
        var resourceGroup = request.Query["resourceGroup"];

        if (string.IsNullOrWhiteSpace(subscription) || string.IsNullOrWhiteSpace(resourceGroup))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { success = false, error = "Query parameters 'subscription' and 'resourceGroup' are required." }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return badRequest;
        }

        try
        {
            // Note: SDK returns ResourceItem for resource entries per the connector API schema
            var resources = new List<ResourceItem>();
            await foreach (var resource in this._logAnalyticsClient
                .ListResourcesAsync(subscription: subscription, resourceGroup: resourceGroup, resourceType: AzureLogAnalyticsFunctions.OperationalInsightsWorkspaceResourceType)
                .WithCancellation(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false))
            {
                resources.Add(resource);
            }

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, count = resources.Count, workspaces = resources }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "ListLogAnalyticsWorkspaces failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in ListLogAnalyticsWorkspaces.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Runs a query against an Azure Monitor Logs resource.
    /// </summary>
    [Function("QueryLogAnalyticsData")]
    public async Task<HttpResponseData> QueryDataAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "loganalytics/query")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var subscription = request.Query["subscription"];
        var resourceGroup = request.Query["resourceGroup"];
        var resourceType = request.Query["resourceType"];
        var resourceName = request.Query["resourceName"];

        if (!AzureLogAnalyticsFunctions.HasRequiredResourceParameters(subscription, resourceGroup, resourceType, resourceName))
        {
            return await AzureLogAnalyticsFunctions
                .CreateBadRequestAsync(
                    request,
                    message: "Query parameters 'subscription', 'resourceGroup', 'resourceType', and 'resourceName' are required.",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }

        QueryDataInput? input;
        try
        {
            input = await JsonSerializer
                .DeserializeAsync<QueryDataInput>(request.Body, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (JsonException ex)
        {
            this._logger.LogWarning(ex, "Unable to deserialize the Azure Monitor Logs query request body.");
            input = null;
        }

        if (input is null || string.IsNullOrWhiteSpace(input.Query))
        {
            return await AzureLogAnalyticsFunctions
                .CreateBadRequestAsync(
                    request,
                    message: "Request body must contain a non-empty 'query' value.",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }

        try
        {
            var result = await this._logAnalyticsClient
                .QueryDataAsync(
                    input: input,
                    subscription: subscription,
                    resourceGroup: resourceGroup,
                    resourceType: resourceType,
                    resourceName: resourceName,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, count = result.Value?.Count ?? 0, rows = result.Value }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "QueryLogAnalyticsData failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in QueryLogAnalyticsData.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Gets the dynamic schema for an Azure Monitor Logs query.
    /// </summary>
    [Function("QueryLogAnalyticsSchema")]
    public async Task<HttpResponseData> QuerySchemaAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "loganalytics/queryschema")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var subscription = request.Query["subscription"];
        var resourceGroup = request.Query["resourceGroup"];
        var resourceType = request.Query["resourceType"];
        var resourceName = request.Query["resourceName"];

        if (!AzureLogAnalyticsFunctions.HasRequiredResourceParameters(subscription, resourceGroup, resourceType, resourceName))
        {
            return await AzureLogAnalyticsFunctions
                .CreateBadRequestAsync(
                    request,
                    message: "Query parameters 'subscription', 'resourceGroup', 'resourceType', and 'resourceName' are required.",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }

        using var reader = new StreamReader(request.Body, leaveOpen: true);
        var query = await reader
            .ReadToEndAsync(cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (string.IsNullOrWhiteSpace(query))
        {
            return await AzureLogAnalyticsFunctions
                .CreateBadRequestAsync(
                    request,
                    message: "Request body must contain a non-empty KQL query.",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }

        try
        {
            var result = await this._logAnalyticsClient
                .QuerySchemaAsync(
                    input: query,
                    subscription: subscription,
                    resourceGroup: resourceGroup,
                    resourceType: resourceType,
                    resourceName: resourceName,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, schema = result.AdditionalProperties }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "QueryLogAnalyticsSchema failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in QueryLogAnalyticsSchema.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    private static bool HasRequiredResourceParameters(
        string? subscription,
        string? resourceGroup,
        string? resourceType,
        string? resourceName)
    {
        return !string.IsNullOrWhiteSpace(subscription)
            && !string.IsNullOrWhiteSpace(resourceGroup)
            && !string.IsNullOrWhiteSpace(resourceType)
            && !string.IsNullOrWhiteSpace(resourceName);
    }

    private static async Task<HttpResponseData> CreateBadRequestAsync(
        HttpRequestData request,
        string message,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(HttpStatusCode.BadRequest);
        await response
            .WriteAsJsonAsync(new { success = false, error = message }, cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return response;
    }
}
