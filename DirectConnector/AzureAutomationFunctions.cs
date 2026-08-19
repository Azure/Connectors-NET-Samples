//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

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
}