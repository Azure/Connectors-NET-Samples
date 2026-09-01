//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using Azure.Connectors.Sdk.AzureVM;
using Azure.Connectors.Sdk.AzureVM.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating read-only Azure Virtual Machines discovery.
/// </summary>
public class AzureVMFunctions
{
    private readonly AzureVMClient _client;
    private readonly ILogger<AzureVMFunctions> _logger;

    public AzureVMFunctions(ILogger<AzureVMFunctions> logger, AzureVMClient client)
    {
        this._logger = logger;
        this._client = client;
    }

    [Function("AzureVMListSubscriptions")]
    public Task<HttpResponseData> ListSubscriptionsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "azurevm/subscriptions")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        return ConnectorFunctionExecutor.ExecuteAsync(
            request,
            this._logger,
            operationName: "AzureVMListSubscriptions",
            operation: async () =>
            {
                var subscriptions = new List<Subscription>();
                await foreach (var subscription in this._client
                    .SubscriptionsListAsync(cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false))
                {
                    subscriptions.Add(subscription);
                }

                return subscriptions;
            },
            cancellationToken);
    }

    [Function("AzureVMGetVirtualMachine")]
    public Task<HttpResponseData> GetVirtualMachineAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "azurevm/virtualmachine")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var subscriptionId = request.Query["subscriptionId"];
        var resourceGroup = request.Query["resourceGroup"];
        var virtualMachine = request.Query["virtualMachine"];
        if (string.IsNullOrWhiteSpace(subscriptionId) ||
            string.IsNullOrWhiteSpace(resourceGroup) ||
            string.IsNullOrWhiteSpace(virtualMachine))
        {
            return AzureVMFunctions.CreateBadRequestAsync(request, cancellationToken);
        }

        return ConnectorFunctionExecutor.ExecuteAsync(
            request,
            this._logger,
            operationName: "AzureVMGetVirtualMachine",
            operation: () => this._client
                .VirtualMachineGetAsync(
                    subscriptionId: subscriptionId,
                    resourceGroup: resourceGroup,
                    virtualMachine: virtualMachine,
                    cancellationToken: cancellationToken),
            cancellationToken);
    }

    private static async Task<HttpResponseData> CreateBadRequestAsync(
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(System.Net.HttpStatusCode.BadRequest);
        await response
            .WriteAsJsonAsync(
                new { success = false, error = "Query parameters 'subscriptionId', 'resourceGroup', and 'virtualMachine' are required." },
                cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        return response;
    }
}