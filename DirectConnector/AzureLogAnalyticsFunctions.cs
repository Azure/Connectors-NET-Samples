//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
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
}
