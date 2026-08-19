//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using Azure.Connectors.Sdk.Office365GroupsMail;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating read-only Microsoft 365 Groups Mail operations.
/// </summary>
public class Office365GroupsMailFunctions
{
    private readonly Office365GroupsMailClient _client;
    private readonly ILogger<Office365GroupsMailFunctions> _logger;

    public Office365GroupsMailFunctions(ILogger<Office365GroupsMailFunctions> logger, Office365GroupsMailClient client)
    {
        this._logger = logger;
        this._client = client;
    }

    [Function("Office365GroupsMailListGroups")]
    public Task<HttpResponseData> ListGroupsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "office365groupsmail/groups")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        return ConnectorFunctionExecutor.ExecuteAsync(
            request,
            this._logger,
            operationName: "Office365GroupsMailListGroups",
            operation: () => this._client.ListGroupsAsync(cancellationToken),
            cancellationToken);
    }
}