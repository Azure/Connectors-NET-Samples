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

    [Function("PowerBIListScorecards")]
    public Task<HttpResponseData> ListScorecardsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "powerbi/scorecards")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var workspace = request.Query["workspace"];
        if (string.IsNullOrWhiteSpace(workspace))
        {
            return PowerBIFunctions.CreateBadRequestAsync(request, cancellationToken);
        }

        return ConnectorFunctionExecutor.ExecuteAsync(
            request,
            this._logger,
            operationName: "PowerBIListScorecards",
            operation: () => this._client.GetScorecardsAsync(workspace, cancellationToken),
            cancellationToken);
    }

    private static async Task<HttpResponseData> CreateBadRequestAsync(
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(System.Net.HttpStatusCode.BadRequest);
        await response
            .WriteAsJsonAsync(new { success = false, error = "Query parameter 'workspace' is required." }, cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        return response;
    }
}