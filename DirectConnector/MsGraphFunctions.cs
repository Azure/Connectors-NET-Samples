//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using Microsoft.Azure.Connectors.DirectClient.Msgraphgroupsanduser;
using Microsoft.Azure.Connectors.Sdk;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating MS Graph Groups &amp; Users operations using the generated
/// <see cref="MsgraphgroupsanduserClient"/> from the DirectClient SDK.
/// </summary>
/// <remarks>
/// Exercises user listing, group search, and group property retrieval.
/// </remarks>
public class MsGraphFunctions
{
    private readonly ILogger<MsGraphFunctions> _logger;
    private readonly MsgraphgroupsanduserClient _msGraphClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="MsGraphFunctions"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="msGraphClient">The DI-injected MS Graph Groups &amp; Users client (disposed by the host).</param>
    public MsGraphFunctions(
        ILogger<MsGraphFunctions> logger,
        MsgraphgroupsanduserClient msGraphClient)
    {
        this._logger = logger;
        this._msGraphClient = msGraphClient;
    }

    /// <summary>
    /// Lists a page of users in the tenant using the generated <see cref="MsgraphgroupsanduserClient"/>.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("ListGraphUsers")]
    public async Task<HttpResponseData> ListGraphUsersAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "graph/users")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("ListGraphUsers: Using generated MsgraphgroupsanduserClient from SDK.");

        try
        {
            var users = await this._msGraphClient
                .ListUsersAsync(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    count = users?.Value?.Count ?? 0,
                    users = users
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (MsgraphgroupsanduserConnectorException ex)
        {
            this._logger.LogError(ex, "MS Graph connector error: '{StatusCode}'.", ex.StatusCode);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message,
                    statusCode = ex.StatusCode,
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
    /// Searches for groups by display name using the generated <see cref="MsgraphgroupsanduserClient"/>.
    /// </summary>
    /// <param name="request">The HTTP request with optional 'search' query parameter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("ListGraphGroups")]
    public async Task<HttpResponseData> ListGraphGroupsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "graph/groups")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("ListGraphGroups: Using generated MsgraphgroupsanduserClient from SDK.");

        var rawSearch = request.Query["search"];
        var search = string.IsNullOrWhiteSpace(rawSearch) ? null : rawSearch.ToString();

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
        catch (MsgraphgroupsanduserConnectorException ex)
        {
            this._logger.LogError(ex, "MS Graph connector error: '{StatusCode}'.", ex.StatusCode);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message,
                    statusCode = ex.StatusCode,
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
    /// Gets properties of a specific group using the generated <see cref="MsgraphgroupsanduserClient"/>.
    /// </summary>
    /// <param name="request">The HTTP request with 'groupId' query parameter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("GetGraphGroupProperties")]
    public async Task<HttpResponseData> GetGraphGroupPropertiesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "graph/groups/properties")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("GetGraphGroupProperties: Using generated MsgraphgroupsanduserClient from SDK.");

        var groupId = request.Query["groupId"];
        if (string.IsNullOrEmpty(groupId))
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
                .GetGroupPropertiesAsync(objectIDOfTheMicrosoftEntraIDGroup: groupId, cancellationToken: cancellationToken)
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
        catch (MsgraphgroupsanduserConnectorException ex)
        {
            this._logger.LogError(ex, "MS Graph connector error: '{StatusCode}'.", ex.StatusCode);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message,
                    statusCode = ex.StatusCode,
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
