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

    [Function("Office365GroupsMailListConversations")]
    public Task<HttpResponseData> ListConversationsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "office365groupsmail/conversations")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var groupId = request.Query["groupId"];
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return Office365GroupsMailFunctions.CreateBadRequestAsync(request, cancellationToken);
        }

        return ConnectorFunctionExecutor.ExecuteAsync(
            request,
            this._logger,
            operationName: "Office365GroupsMailListConversations",
            operation: async () =>
            {
                var conversations = new List<Azure.Connectors.Sdk.Office365GroupsMail.Models.Conversation>();
                await foreach (var page in this._client
                    .ListConversationsAsync(groupId, cancellationToken)
                    .AsPages(pageSizeHint: 20)
                    .WithCancellation(cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false))
                {
                    conversations.AddRange(page.Values);
                    break;
                }

                return conversations;
            },
            cancellationToken);
    }

    private static async Task<HttpResponseData> CreateBadRequestAsync(
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(System.Net.HttpStatusCode.BadRequest);
        await response
            .WriteAsJsonAsync(new { success = false, error = "Query parameter 'groupId' is required." }, cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        return response;
    }
}