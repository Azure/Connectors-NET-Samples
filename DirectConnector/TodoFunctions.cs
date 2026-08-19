//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using Azure.Connectors.Sdk.Todo;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating read-only Microsoft To Do operations.
/// </summary>
public class TodoFunctions
{
    private readonly TodoClient _client;
    private readonly ILogger<TodoFunctions> _logger;

    public TodoFunctions(ILogger<TodoFunctions> logger, TodoClient client)
    {
        this._logger = logger;
        this._client = client;
    }

    [Function("TodoListTaskLists")]
    public Task<HttpResponseData> ListTaskListsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "todo/lists")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        return ConnectorFunctionExecutor.ExecuteAsync(
            request,
            this._logger,
            operationName: "TodoListTaskLists",
            operation: () => this._client.GetAllTodoListsAsync(cancellationToken),
            cancellationToken);
    }
}