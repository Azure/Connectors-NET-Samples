//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Text.Json;
using Azure.Connectors.Sdk.AzureDigitalTwins;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating read-only Azure Digital Twins operations.
/// </summary>
public class AzureDigitalTwinsFunctions
{
    private readonly AzureDigitalTwinsClient _client;
    private readonly ILogger<AzureDigitalTwinsFunctions> _logger;

    public AzureDigitalTwinsFunctions(ILogger<AzureDigitalTwinsFunctions> logger, AzureDigitalTwinsClient client)
    {
        this._logger = logger;
        this._client = client;
    }

    [Function("AzureDigitalTwinsListModels")]
    public Task<HttpResponseData> ListModelsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "azuredigitaltwins/models")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        return ConnectorFunctionExecutor.ExecuteAsync(
            request,
            this._logger,
            operationName: "AzureDigitalTwinsListModels",
            operation: async () =>
            {
                var models = new List<JsonElement?>();
                await foreach (var model in this._client.ListModelsAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false))
                {
                    models.Add(model);
                }

                return models;
            },
            cancellationToken);
    }
}