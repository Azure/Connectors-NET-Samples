//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using Azure.Connectors.Sdk.PowerBI;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating read-only Power BI operations.
/// </summary>
public class PowerBIFunctions
{
    private readonly PowerBIClient _client;
    private readonly ILogger<PowerBIFunctions> _logger;

    public PowerBIFunctions(ILogger<PowerBIFunctions> logger, PowerBIClient client)
    {
        this._logger = logger;
        this._client = client;
    }

    [Function("PowerBIListWorkspaces")]
    public Task<HttpResponseData> ListWorkspacesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "powerbi/workspaces")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        return ConnectorFunctionExecutor.ExecuteAsync(
            request,
            this._logger,
            operationName: "PowerBIListWorkspaces",
            operation: () => this._client.ListGroupsAsync(cancellationToken),
            cancellationToken);
    }
}