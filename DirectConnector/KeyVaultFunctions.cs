//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using Azure.Connectors.Sdk.KeyVault;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating Key Vault metadata operations without reading secret values.
/// </summary>
public class KeyVaultFunctions
{
    private readonly KeyVaultClient _client;
    private readonly ILogger<KeyVaultFunctions> _logger;

    public KeyVaultFunctions(ILogger<KeyVaultFunctions> logger, KeyVaultClient client)
    {
        this._logger = logger;
        this._client = client;
    }

    [Function("KeyVaultListSecrets")]
    public Task<HttpResponseData> ListSecretsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "keyvault/secrets")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        return ConnectorFunctionExecutor.ExecuteAsync(
            request,
            this._logger,
            operationName: "KeyVaultListSecrets",
            operation: () => this._client.ListSecretsAsync(cancellationToken),
            cancellationToken);
    }
}