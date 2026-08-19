//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using Azure.Connectors.Sdk.Shifts;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating read-only Microsoft Shifts operations.
/// </summary>
public class ShiftsFunctions
{
    private readonly ShiftsClient _client;
    private readonly ILogger<ShiftsFunctions> _logger;

    public ShiftsFunctions(ILogger<ShiftsFunctions> logger, ShiftsClient client)
    {
        this._logger = logger;
        this._client = client;
    }

    [Function("ShiftsListTeams")]
    public Task<HttpResponseData> ListTeamsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "shifts/teams")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        return ConnectorFunctionExecutor.ExecuteAsync(
            request,
            this._logger,
            operationName: "ShiftsListTeams",
            operation: () => this._client.GetAllTeamsAsync(cancellationToken),
            cancellationToken);
    }
}