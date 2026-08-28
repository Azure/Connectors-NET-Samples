//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Text.Json;
using Azure.Connectors.Sdk.AzureAutomation;
using Azure.Connectors.Sdk.AzureAutomation.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating read-only Azure Automation discovery.
/// </summary>
public class AzureAutomationFunctions
{
    private readonly AzureAutomationClient _client;
    private readonly ILogger<AzureAutomationFunctions> _logger;

    public AzureAutomationFunctions(ILogger<AzureAutomationFunctions> logger, AzureAutomationClient client)
    {
        this._logger = logger;
        this._client = client;
    }

    [Function("AzureAutomationListSubscriptions")]
    public Task<HttpResponseData> ListSubscriptionsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "azureautomation/subscriptions")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        return ConnectorFunctionExecutor.ExecuteAsync(
            request,
            this._logger,
            operationName: "AzureAutomationListSubscriptions",
            operation: async () =>
            {
                var subscriptions = new List<Subscription>();
                await foreach (var subscription in this._client.SubscriptionsListAsync(cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false))
                {
                    subscriptions.Add(subscription);
                }

                return subscriptions;
            },
            cancellationToken);
    }

    [Function("AzureAutomationGetJobStatus")]
    public Task<HttpResponseData> GetJobStatusAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "azureautomation/jobs/status")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var subscriptionId = request.Query["subscriptionId"];
        var resourceGroup = request.Query["resourceGroup"];
        var automationAccount = request.Query["automationAccount"];
        var jobId = request.Query["jobId"];
        if (string.IsNullOrWhiteSpace(subscriptionId) ||
            string.IsNullOrWhiteSpace(resourceGroup) ||
            string.IsNullOrWhiteSpace(automationAccount) ||
            string.IsNullOrWhiteSpace(jobId))
        {
            return AzureAutomationFunctions.CreateBadRequestAsync(
                request,
                requiredParameterName: "jobId",
                cancellationToken);
        }

        return ConnectorFunctionExecutor.ExecuteAsync(
            request,
            this._logger,
            operationName: "AzureAutomationGetJobStatus",
            operation: () => this._client.GetStatusOfJobAsync(
                subscription: subscriptionId,
                resourceGroup: resourceGroup,
                automationAccount: automationAccount,
                jobId: jobId,
                cancellationToken: cancellationToken),
            cancellationToken);
    }

    [Function("AzureAutomationCreateJob")]
    public Task<HttpResponseData> CreateJobAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "azureautomation/jobs")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var subscriptionId = request.Query["subscriptionId"];
        var resourceGroup = request.Query["resourceGroup"];
        var automationAccount = request.Query["automationAccount"];
        var runbookName = request.Query["runbookName"];
        if (string.IsNullOrWhiteSpace(subscriptionId) ||
            string.IsNullOrWhiteSpace(resourceGroup) ||
            string.IsNullOrWhiteSpace(automationAccount) ||
            string.IsNullOrWhiteSpace(runbookName))
        {
            return AzureAutomationFunctions.CreateBadRequestAsync(
                request,
                requiredParameterName: "runbookName",
                cancellationToken);
        }

        var input = new CreateJobInput
        {
            Properties = JsonSerializer.SerializeToElement(new { parameters = new { } }),
        };
        return ConnectorFunctionExecutor.ExecuteAsync(
            request,
            this._logger,
            operationName: "AzureAutomationCreateJob",
            operation: () => this._client.CreateJobAsync(
                subscription: subscriptionId,
                resourceGroup: resourceGroup,
                automationAccount: automationAccount,
                input: input,
                runbookName: runbookName,
                waitForJob: false,
                cancellationToken: cancellationToken),
            cancellationToken);
    }

    private static async Task<HttpResponseData> CreateBadRequestAsync(
        HttpRequestData request,
        string requiredParameterName,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(System.Net.HttpStatusCode.BadRequest);
        await response
            .WriteAsJsonAsync(
                new { success = false, error = $"Query parameters 'subscriptionId', 'resourceGroup', 'automationAccount', and '{requiredParameterName}' are required." },
                cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        return response;
    }
}