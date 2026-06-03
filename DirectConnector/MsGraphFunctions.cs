//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.MsGraphGroupsAndUsers;
using Azure.Connectors.Sdk.MsGraphGroupsAndUsers.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating MS Graph Groups &amp; Users operations using the generated
/// <see cref="MsGraphGroupsAndUsersClient"/> from the Azure Connectors SDK.
/// </summary>
/// <remarks>
/// Exercises user listing, group search, and group property retrieval.
/// </remarks>
public class MsGraphFunctions
{
    private readonly ILogger<MsGraphFunctions> _logger;
    private readonly MsGraphGroupsAndUsersClient _msGraphClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="MsGraphFunctions"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="msGraphClient">The DI-injected MS Graph Groups &amp; Users client (disposed by the host).</param>
    public MsGraphFunctions(
        ILogger<MsGraphFunctions> logger,
        MsGraphGroupsAndUsersClient msGraphClient)
    {
        this._logger = logger;
        this._msGraphClient = msGraphClient;
    }

    /// <summary>
    /// Lists a page of users in the tenant using the generated <see cref="MsGraphGroupsAndUsersClient"/>.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("ListGraphUsers")]
    public async Task<HttpResponseData> ListGraphUsersAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "graph/users")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("ListGraphUsers: Using generated MsGraphGroupsAndUsersClient from SDK.");

        try
        {
            var users = await this._msGraphClient
                .ListUsersAsync(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            // SDK v0.12.0: Dynamic model properties are now JsonElement? (was object).
            // List<JsonElement?> requires explicit extraction of typed values.
            // Use GetProperty/GetString to navigate the free-form JSON structure.
            var userSummaries = users?.Value?
                .Where(element => element.HasValue)
                .Select(element =>
                {
                    var user = element!.Value;
                    return new
                    {
                        displayName = user.TryGetProperty("displayName", out var dn) ? dn.GetString() : null,
                        mail = user.TryGetProperty("mail", out var m) ? m.GetString() : null,
                        id = user.TryGetProperty("id", out var id) ? id.GetString() : null,
                    };
                })
                .ToList();

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    count = userSummaries?.Count ?? 0,
                    users = userSummaries
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            // SDK v0.12.0: ErrorCode is parsed from the connector's JSON error response.
            this._logger.LogError(
                ex,
                "MS Graph connector error: Status={Status}, ErrorCode='{ErrorCode}'.",
                ex.Status,
                ex.ErrorCode);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message,
                    errorCode = ex.ErrorCode,
                    statusCode = ex.Status,
                    details = ex.ResponseBody
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in ListGraphUsers.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Searches for groups by display name using the generated <see cref="MsGraphGroupsAndUsersClient"/>.
    /// </summary>
    /// <param name="request">The HTTP request with optional 'search' query parameter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("ListGraphGroups")]
    public async Task<HttpResponseData> ListGraphGroupsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "graph/groups")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("ListGraphGroups: Using generated MsGraphGroupsAndUsersClient from SDK.");

        var rawSearch = request.Query["search"];
        var search = string.IsNullOrWhiteSpace(rawSearch) ? null : rawSearch;

        try
        {
            var groups = await this._msGraphClient
                .ListGroupsByDisplayNameSearchAsync(searchByDisplayName: search, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    search = search ?? "(all)",
                    count = groups?.Value?.Count ?? 0,
                    groups = groups
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "MS Graph connector error: '{StatusCode}'.", ex.Status);

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
            this._logger.LogError(ex, "Error in ListGraphGroups.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Gets properties of a specific group using the generated <see cref="MsGraphGroupsAndUsersClient"/>.
    /// </summary>
    /// <param name="request">The HTTP request with 'groupId' query parameter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("GetGraphGroupProperties")]
    public async Task<HttpResponseData> GetGraphGroupPropertiesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "graph/groups/properties")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("GetGraphGroupProperties: Using generated MsGraphGroupsAndUsersClient from SDK.");

        var rawGroupId = request.Query["groupId"];
        var groupId = rawGroupId?.Trim();
        if (string.IsNullOrWhiteSpace(groupId))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { success = false, error = "Query parameter 'groupId' is required." })
                .ConfigureAwait(continueOnCapturedContext: false);
            return badRequest;
        }

        try
        {
            var group = await this._msGraphClient
                .GetGroupPropertiesAsync(objectIdOfTheMicrosoftEntraIdGroup: groupId, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    groupId = group?.Id,
                    displayName = group?.DisplayName,
                    description = group?.Description,
                    mail = group?.Mail,
                    mailEnabled = group?.MailEnabled,
                    securityEnabled = group?.SecurityEnabled,
                    visibility = group?.Visibility
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "MS Graph connector error: '{StatusCode}'.", ex.Status);

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
            this._logger.LogError(ex, "Error in GetGraphGroupProperties.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }
}
