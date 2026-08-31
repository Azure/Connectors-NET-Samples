//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Text.Json;
using Azure.Connectors.Sdk.Office365Groups;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating read-only Microsoft 365 Groups operations.
/// </summary>
public class Office365GroupsFunctions
{
    private readonly Office365GroupsClient _client;
    private readonly ILogger<Office365GroupsFunctions> _logger;

    public Office365GroupsFunctions(ILogger<Office365GroupsFunctions> logger, Office365GroupsClient client)
    {
        this._logger = logger;
        this._client = client;
    }

    [Function("Office365GroupsListGroups")]
    public Task<HttpResponseData> ListGroupsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "office365groups/groups")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        return ConnectorFunctionExecutor.ExecuteAsync(
            request,
            this._logger,
            operationName: "Office365GroupsListGroups",
            operation: async () =>
            {
                var groups = new List<JsonElement?>();
                await foreach (var page in this._client
                    .ListGroupsAsync(pageSize: 20, cancellationToken: cancellationToken)
                    .AsPages(pageSizeHint: 20)
                    .WithCancellation(cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false))
                {
                    groups.AddRange(page.Values);
                }

                return groups;
            },
            cancellationToken);
    }
}