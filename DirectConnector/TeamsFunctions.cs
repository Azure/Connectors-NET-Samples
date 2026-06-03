//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.Teams;
using Azure.Connectors.Sdk.Teams.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions that use the generated <see cref="TeamsClient"/> from the Azure Connectors SDK.
/// </summary>
/// <remarks>
/// Demonstrates listing teams, channels, channel messages with IAsyncEnumerable pagination,
/// posting messages, and CancellationToken propagation from the Functions host.
/// </remarks>
public class TeamsFunctions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Default poster identity for Teams messages posted via the connector.
    /// </summary>
    private const string TeamsDefaultPoster = "Flow bot";

    /// <summary>
    /// Default message location for Teams channel posts.
    /// </summary>
    private const string TeamsDefaultLocation = "Channel";

    private readonly ILogger<TeamsFunctions> _logger;
    private readonly TeamsClient _teamsClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamsFunctions"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="teamsClient">The DI-injected Teams client (disposed by the host).</param>
    public TeamsFunctions(
        ILogger<TeamsFunctions> logger,
        TeamsClient teamsClient)
    {
        this._logger = logger;
        this._teamsClient = teamsClient;
    }

    /// <summary>
    /// Lists all Teams the signed-in user is a member of using the generated <see cref="TeamsClient"/>.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("GetAllTeams")]
    public async Task<HttpResponseData> GetAllTeamsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "teams/teams")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("GetAllTeams: Using generated TeamsClient from SDK.");

        try
        {
            var result = await this._teamsClient
                .GetAllTeamsAsync(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    count = result?.TeamsList?.Count ?? 0,
                    teams = result?.TeamsList
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "Teams connector error: '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message,
                    statusCode = ex.Status,
                    details = ex.ResponseBody
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in GetAllTeams.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Lists all channels for a specific team using the generated <see cref="TeamsClient"/>.
    /// </summary>
    /// <param name="request">The HTTP request containing the team ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("GetTeamChannels")]
    public async Task<HttpResponseData> GetTeamChannelsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "teams/channels")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("GetTeamChannels: Using generated TeamsClient from SDK.");

        var teamId = request.Query["teamId"];
        if (string.IsNullOrEmpty(teamId))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { error = "Query parameter 'teamId' is required." })
                .ConfigureAwait(continueOnCapturedContext: false);
            return badRequest;
        }

        try
        {
            var result = await this._teamsClient
                .GetChannelsForGroupAsync(teamId, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    teamId,
                    count = result?.ChannelList?.Count ?? 0,
                    channels = result?.ChannelList?.Select(channel => new
                    {
                        id = channel.ChannelId,
                        displayName = channel.DisplayName,
                        description = channel.DescriptionOfChannel,
                        membershipType = channel.TheTypeOfTheChannel
                    })
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "Teams connector error: '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message,
                    statusCode = ex.Status,
                    details = ex.ResponseBody
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in GetTeamChannels.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Gets all messages from a Teams channel, automatically paginating across all pages.
    /// </summary>
    /// <remarks>
    /// Demonstrates <see cref="IAsyncEnumerable{T}"/> pagination: <c>GetMessagesFromChannelAsync</c>
    /// returns a <c>ConnectorPageable</c> that follows <c>@odata.nextLink</c> automatically.
    /// The caller uses <c>await foreach</c> and never sees pagination details.
    /// </remarks>
    /// <param name="request">The HTTP request with teamId and channelId query parameters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("GetChannelMessages")]
    public async Task<HttpResponseData> GetChannelMessagesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "teams/messages")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("GetChannelMessages: Demonstrating IAsyncEnumerable pagination.");

        var teamId = request.Query["teamId"];
        var channelId = request.Query["channelId"];

        if (string.IsNullOrEmpty(teamId) || string.IsNullOrEmpty(channelId))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { error = "Query parameters 'teamId' and 'channelId' are required." })
                .ConfigureAwait(continueOnCapturedContext: false);
            return badRequest;
        }

        try
        {
            // GetMessagesFromChannelAsync returns IAsyncEnumerable<ChatMessage> that automatically
            // follows @odata.nextLink pagination across all pages.
            var messages = new List<object>();
            await foreach (var message in this._teamsClient
                .GetMessagesFromChannelAsync(teamId, channelId)
                .WithCancellation(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false))
            {
                messages.Add(new
                {
                    id = message.Id,
                    subject = message.Subject,
                    messageType = message.MessageType,
                    createdDateTime = message.CreationTimestamp,
                    from = message.From
                });
            }

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    teamId,
                    channelId,
                    totalMessages = messages.Count,
                    messages
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "Teams connector error: '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message,
                    statusCode = ex.Status,
                    details = ex.ResponseBody
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in GetChannelMessages.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Posts a message to a Teams channel using the generated <see cref="TeamsClient"/>.
    /// </summary>
    /// <param name="request">The HTTP request containing team, channel, and message details.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("PostTeamsMessage")]
    public async Task<HttpResponseData> PostTeamsMessageAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "teams/message")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("PostTeamsMessage: Using generated TeamsClient from SDK.");

        try
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader
                .ReadToEndAsync(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            PostTeamsMessageRequest? input;
            try
            {
                input = JsonSerializer.Deserialize<PostTeamsMessageRequest>(body, TeamsFunctions.JsonOptions);
            }
            catch (JsonException ex)
            {
                this._logger.LogError(ex, "Invalid JSON in request body: '{Message}'.", ex.Message);

                var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest
                    .WriteAsJsonAsync(new { error = "Request body must contain valid JSON." })
                    .ConfigureAwait(continueOnCapturedContext: false);

                return badRequest;
            }

            if (input == null || string.IsNullOrEmpty(input.TeamId) ||
                string.IsNullOrEmpty(input.ChannelId) || string.IsNullOrEmpty(input.Message))
            {
                var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest
                    .WriteAsJsonAsync(new { error = "Fields 'teamId', 'channelId', and 'message' are required." })
                    .ConfigureAwait(continueOnCapturedContext: false);
                return badRequest;
            }

            // NOTE: PostMessageToConversationAsync uses DynamicPostMessageRequest (dynamic schema).
            // The actual message body properties are determined at runtime by the connector's schema
            // discovery endpoint. With [JsonExtensionData] on AdditionalProperties, arbitrary properties
            // are now serialized correctly. Populate the dictionary with the expected message fields.
            var messageRequest = new DynamicPostMessageRequest();
            messageRequest.AdditionalProperties["recipient"] = JsonSerializer.SerializeToElement(
                new
                {
                    groupId = input.TeamId,
                    channelId = input.ChannelId,
                });
            messageRequest.AdditionalProperties["messageBody"] = JsonSerializer.SerializeToElement(
                $"<p>{WebUtility.HtmlEncode(input.Message)}</p>");

            var result = await this._teamsClient
                .PostMessageToConversationAsync(
                    postAs: TeamsFunctions.TeamsDefaultPoster,
                    postIn: TeamsFunctions.TeamsDefaultLocation,
                    input: messageRequest,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    message = "Message posted to Teams channel via generated TeamsClient from SDK.",
                    messageId = result?.MessageId,
                    messageLink = result?.MessageLink,
                    timestamp = DateTime.UtcNow
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "Teams connector error: '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message,
                    statusCode = ex.Status,
                    details = ex.ResponseBody
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in PostTeamsMessage.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Request model for posting a Teams message.
    /// </summary>
    private record PostTeamsMessageRequest(string? TeamId, string? ChannelId, string? Message);

    /// <summary>
    /// Trigger callback for Teams channel messages (v0.12.0 typed trigger payload).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Demonstrates the <see cref="TeamsOnNewChannelMessageTriggerPayload"/> typed trigger payload
    /// added in SDK v0.12.0. Connector Namespace calls this endpoint when a new message is posted
    /// to a subscribed channel.
    /// </para>
    /// <para>
    /// Also demonstrates <see cref="TeamsTriggers.Operations"/> — the static registry that maps
    /// trigger operation names to their typed payload types for dynamic dispatch scenarios.
    /// </para>
    /// </remarks>
    [Function("TeamsChannelMessageTrigger")]
    [ConnectorTriggerMetadata(
        ConnectorName = ConnectorNames.MicrosoftTeams,
        OperationName = TeamsTriggerOperations.OnNewChannelMessage,
        Connection = "Connectors:Teams")]
    public async Task<HttpResponseData> TeamsChannelMessageTriggerAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "teams/trigger/channelmessage")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("TeamsChannelMessageTrigger: Callback received.");

        var body = await request
            .ReadAsStringAsync()
            .ConfigureAwait(continueOnCapturedContext: false);

        if (string.IsNullOrEmpty(body))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { success = false, error = "Empty trigger payload." })
                .ConfigureAwait(continueOnCapturedContext: false);
            return badRequest;
        }

        TeamsOnNewChannelMessageTriggerPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<TeamsOnNewChannelMessageTriggerPayload>(
                body,
                TeamsFunctions.JsonOptions);
        }
        catch (JsonException ex)
        {
            this._logger.LogError(ex, "TeamsChannelMessageTrigger: Invalid JSON in trigger payload.");

            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { success = false, error = "Trigger payload is not valid JSON." })
                .ConfigureAwait(continueOnCapturedContext: false);
            return badRequest;
        }

        if (payload is null)
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { success = false, error = "Trigger payload deserialized to null." })
                .ConfigureAwait(continueOnCapturedContext: false);
            return badRequest;
        }

        var messages = payload?.Body?.Value;
        var messageCount = messages?.Count ?? 0;

        this._logger.LogInformation(
            "TeamsChannelMessageTrigger: Deserialized {Count} message(s) using TeamsOnNewChannelMessageTriggerPayload.",
            messageCount);

        // NOTE: TeamsTriggers.Operations maps operation names to payload types at runtime.
        // Useful for dynamic dispatch when the operation name comes from configuration.
        if (TeamsTriggers.Operations.TryGetValue(
            TeamsTriggerOperations.OnNewChannelMessage, out var payloadType))
        {
            this._logger.LogDebug(
                "TeamsChannelMessageTrigger: Operation '{Operation}' maps to payload type '{Type}'.",
                TeamsTriggerOperations.OnNewChannelMessage,
                payloadType.Name);
        }

        var response = request.CreateResponse(HttpStatusCode.OK);
        await response
            .WriteAsJsonAsync(new
            {
                success = true,
                message = "Teams channel message trigger callback received.",
                receivedAt = DateTime.UtcNow,
                messageCount
            })
            .ConfigureAwait(continueOnCapturedContext: false);

        return response;
    }

    /// <summary>
    /// Demonstrates <see cref="ConnectorException.ErrorCode"/> parsing added in SDK v0.12.0.
    /// </summary>
    /// <remarks>
    /// When a connector API returns an error response, <see cref="ConnectorException"/>
    /// now extracts the structured error code from the response body and populates
    /// <see cref="Azure.RequestFailedException.ErrorCode"/>, enabling callers to
    /// switch on error codes rather than parsing error messages.
    /// </remarks>
    [Function("GetTeamsWithErrorHandling")]
    public async Task<HttpResponseData> GetTeamsWithErrorHandlingAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "teams/teams-with-error-handling")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("GetTeamsWithErrorHandling: Listing teams with structured error handling.");

        try
        {
            var teams = await this._teamsClient
                .GetAllTeamsAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, teamCount = teams?.TeamsList?.Count ?? 0 })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            // SDK v0.12.0: ErrorCode is now parsed from the connector's JSON error response.
            // Callers can switch on structured error codes instead of parsing error messages.
            this._logger.LogWarning(
                "GetTeamsWithErrorHandling: Connector error \u2014 Status='{Status}', ErrorCode='{ErrorCode}', Message='{Message}'.",
                ex.Status,
                ex.ErrorCode,
                ex.Message);

            var errorResponse = request.CreateResponse((HttpStatusCode)ex.Status);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    status = ex.Status,
                    errorCode = ex.ErrorCode,
                    message = ex.Message
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }
}
