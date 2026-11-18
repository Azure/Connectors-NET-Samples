//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using Azure.Connectors.Sdk.Shifts;
using Azure.Connectors.Sdk.Shifts.Models;
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
            operation: () => this._client
                .GetAllTeamsAsync(cancellationToken),
            cancellationToken);
    }

    [Function("ShiftsListCrossTeamShifts")]
    public Task<HttpResponseData> ListCrossTeamShiftsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "shifts/crossteam")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        return ConnectorFunctionExecutor.ExecuteAsync(
            request,
            this._logger,
            operationName: "ShiftsListCrossTeamShifts",
            operation: async () =>
            {
                var shifts = new List<ShiftResponse>();
                await foreach (var shift in this._client
                    .ListShiftsCrossTeamAsync(
                        fromStartTime: request.Query["fromStartTime"],
                        toEndTime: request.Query["toEndTime"],
                        userDisplayName: request.Query["userDisplayName"],
                        pageSize: 20,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false))
                {
                    shifts.Add(shift);
                }

                return new ListShiftsCrossTeamResponse { Value = shifts };
            },
            cancellationToken);
    }
}