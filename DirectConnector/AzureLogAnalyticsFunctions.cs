//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Microsoft.Azure.Connectors.DirectClient.Azureloganalytics;
using Microsoft.Azure.Connectors.Sdk;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating Azure Log Analytics operations using the generated
/// <see cref="AzureloganalyticsClient"/> from the DirectClient SDK.
/// </summary>
/// <remarks>
/// Exercises subscription listing, resource group listing, workspace listing,
/// and query schema retrieval. All paginated operations use <c>await foreach</c>
/// via the SDK's <c>ConnectorPageable</c> auto-pagination.
/// </remarks>
public class AzureLogAnalyticsFunctions
{
    private readonly ILogger<AzureLogAnalyticsFunctions> _logger;
    private readonly AzureloganalyticsClient _logAnalyticsClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureLogAnalyticsFunctions"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="logAnalyticsClient">The DI-injected Azure Log Analytics client (disposed by the host).</param>
    public AzureLogAnalyticsFunctions(
        ILogger<AzureLogAnalyticsFunctions> logger,
        AzureloganalyticsClient logAnalyticsClient)
    {
        this._logger = logger;
        this._logAnalyticsClient = logAnalyticsClient;
    }

    /// <summary>
    /// Lists subscriptions available to the authenticated connection.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("ListLogAnalyticsSubscriptions")]
    public async Task<HttpResponseData> ListSubscriptionsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "loganalytics/subscriptions")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("ListLogAnalyticsSubscriptions: Using generated AzureloganalyticsClient from SDK.");

        try
        {
            var subscriptions = new List<Subscription>();
            await foreach (var sub in this._logAnalyticsClient.ListSubscriptionsAsync().WithCancellation(cancellationToken))
            {
                subscriptions.Add(sub);
            }

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    count = subscriptions.Count,
                    subscriptions
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (AzureloganalyticsConnectorException ex)
        {
            this._logger.LogError(ex, "Log Analytics connector error: '{StatusCode}'.", ex.StatusCode);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message,
                    statusCode = ex.StatusCode,
                    details = ex.ResponseBody
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in ListLogAnalyticsSubscriptions.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Lists resource groups for a given subscription.
    /// </summary>
    /// <param name="request">The HTTP request with required 'subscription' query parameter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("ListLogAnalyticsResourceGroups")]
    public async Task<HttpResponseData> ListResourceGroupsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "loganalytics/resourcegroups")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("ListLogAnalyticsResourceGroups: Using generated AzureloganalyticsClient from SDK.");

        var subscription = request.Query["subscription"];
        if (string.IsNullOrWhiteSpace(subscription))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { success = false, error = "Query parameter 'subscription' is required." })
                .ConfigureAwait(continueOnCapturedContext: false);

            return badRequest;
        }

        try
        {
            var resourceGroups = new List<ResourceGroup>();
            await foreach (var rg in this._logAnalyticsClient.ListResourceGroupsAsync(subscription: subscription).WithCancellation(cancellationToken))
            {
                resourceGroups.Add(rg);
            }

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    count = resourceGroups.Count,
                    resourceGroups
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (AzureloganalyticsConnectorException ex)
        {
            this._logger.LogError(ex, "Log Analytics connector error: '{StatusCode}'.", ex.StatusCode);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message,
                    statusCode = ex.StatusCode,
                    details = ex.ResponseBody
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in ListLogAnalyticsResourceGroups.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Lists Log Analytics workspaces in a subscription and resource group.
    /// </summary>
    /// <param name="request">The HTTP request with required 'subscription' and 'resourceGroup' query parameters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("ListLogAnalyticsWorkspaces")]
    public async Task<HttpResponseData> ListWorkspacesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "loganalytics/workspaces")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("ListLogAnalyticsWorkspaces: Using generated AzureloganalyticsClient from SDK.");

        var subscription = request.Query["subscription"];
        var resourceGroup = request.Query["resourceGroup"];

        if (string.IsNullOrWhiteSpace(subscription) || string.IsNullOrWhiteSpace(resourceGroup))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { success = false, error = "Query parameters 'subscription' and 'resourceGroup' are required." })
                .ConfigureAwait(continueOnCapturedContext: false);

            return badRequest;
        }

        try
        {
            var workspaces = new List<ResourceGroup>();
            await foreach (var ws in this._logAnalyticsClient.ListWorkspaceNamesAsync(subscription: subscription, resourceGroup: resourceGroup).WithCancellation(cancellationToken))
            {
                workspaces.Add(ws);
            }

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    count = workspaces.Count,
                    workspaces
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (AzureloganalyticsConnectorException ex)
        {
            this._logger.LogError(ex, "Log Analytics connector error: '{StatusCode}'.", ex.StatusCode);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message,
                    statusCode = ex.StatusCode,
                    details = ex.ResponseBody
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in ListLogAnalyticsWorkspaces.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Retrieves the query schema for a Log Analytics workspace, used to discover available columns.
    /// </summary>
    /// <param name="request">The HTTP request with required 'subscription', 'resourceGroup', and 'workspace' query parameters.
    /// Optional 'query' query parameter for the KQL query (defaults to "Heartbeat | take 1").</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("GetLogAnalyticsQuerySchema")]
    public async Task<HttpResponseData> GetQuerySchemaAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "loganalytics/schema")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("GetLogAnalyticsQuerySchema: Using generated AzureloganalyticsClient from SDK.");

        var subscription = request.Query["subscription"];
        var resourceGroup = request.Query["resourceGroup"];
        var workspace = request.Query["workspace"];
        var query = request.Query["query"] ?? "Heartbeat | take 1";

        if (string.IsNullOrWhiteSpace(subscription) || string.IsNullOrWhiteSpace(resourceGroup) || string.IsNullOrWhiteSpace(workspace))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { success = false, error = "Query parameters 'subscription', 'resourceGroup', and 'workspace' are required." })
                .ConfigureAwait(continueOnCapturedContext: false);

            return badRequest;
        }

        try
        {
            var schema = await this._logAnalyticsClient
                .ListArmQueryResultsSchemaAsync(
                    input: query,
                    subscription: subscription,
                    resourceGroup: resourceGroup,
                    workspacesName: workspace,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    schema
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (AzureloganalyticsConnectorException ex)
        {
            this._logger.LogError(ex, "Log Analytics connector error: '{StatusCode}'.", ex.StatusCode);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message,
                    statusCode = ex.StatusCode,
                    details = ex.ResponseBody
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in GetLogAnalyticsQuerySchema.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }
}
