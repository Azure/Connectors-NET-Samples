//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using Azure.Connectors.Sdk.Onenote;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating read-only OneNote operations.
/// </summary>
public class OnenoteFunctions
{
    private readonly OnenoteClient _client;
    private readonly ILogger<OnenoteFunctions> _logger;

    public OnenoteFunctions(ILogger<OnenoteFunctions> logger, OnenoteClient client)
    {
        this._logger = logger;
        this._client = client;
    }

    [Function("OnenoteListNotebooks")]
    public Task<HttpResponseData> ListNotebooksAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "onenote/notebooks")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        return ConnectorFunctionExecutor.ExecuteAsync(
            request,
            this._logger,
            operationName: "OnenoteListNotebooks",
            operation: () => this._client
                .GetNotebooksAsync(cancellationToken),
            cancellationToken);
    }
}