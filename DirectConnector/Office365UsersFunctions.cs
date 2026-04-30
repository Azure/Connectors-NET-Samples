//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Microsoft.Azure.Connectors.DirectClient.Office365users;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating Office 365 Users operations using the generated
/// <see cref="Office365usersClient"/> from the DirectClient SDK.
/// </summary>
/// <remarks>
/// Exercises user profile lookups, manager/reports chain, and user search.
/// </remarks>
public class Office365UsersFunctions
{
    private readonly ILogger<Office365UsersFunctions> _logger;
    private readonly Office365usersClient _office365UsersClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="Office365UsersFunctions"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="office365UsersClient">The DI-injected Office365 Users client (disposed by the host).</param>
    public Office365UsersFunctions(
        ILogger<Office365UsersFunctions> logger,
        Office365usersClient office365UsersClient)
    {
        this._logger = logger;
        this._office365UsersClient = office365UsersClient;
    }

    /// <summary>
    /// Gets the current user's profile using the generated <see cref="Office365usersClient"/>.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("GetMyProfile")]
    public async Task<HttpResponseData> GetMyProfileAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "users/me")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("GetMyProfile: Using generated Office365usersClient from SDK.");

        try
        {
            var profile = await this._office365UsersClient
                .MyProfileAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(profile, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (Office365usersConnectorException ex)
        {
            this._logger.LogError(ex, "GetMyProfile failed with status {StatusCode}.", ex.StatusCode);

            var errorResponse = request.CreateResponse((HttpStatusCode)ex.StatusCode);
            await errorResponse
                .WriteStringAsync(ex.ResponseBody, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Gets a specific user's profile by UPN using the generated <see cref="Office365usersClient"/>.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="userUPN">The user principal name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("GetUserProfile")]
    public async Task<HttpResponseData> GetUserProfileAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "users/{userUPN}")] HttpRequestData request,
        string userUPN,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("GetUserProfile: Looking up user {UserUPN}.", userUPN);

        try
        {
            var profile = await this._office365UsersClient
                .UserProfileAsync(userUPN: userUPN, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(profile, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (Office365usersConnectorException ex)
        {
            this._logger.LogError(ex, "GetUserProfile failed with status {StatusCode}.", ex.StatusCode);

            var errorResponse = request.CreateResponse((HttpStatusCode)ex.StatusCode);
            await errorResponse
                .WriteStringAsync(ex.ResponseBody, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Gets the manager of a specified user using the generated <see cref="Office365usersClient"/>.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="userUPN">The user principal name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("GetManager")]
    public async Task<HttpResponseData> GetManagerAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "users/{userUPN}/manager")] HttpRequestData request,
        string userUPN,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("GetManager: Looking up manager for {UserUPN}.", userUPN);

        try
        {
            var manager = await this._office365UsersClient
                .ManagerAsync(userUPN: userUPN, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(manager, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (Office365usersConnectorException ex)
        {
            this._logger.LogError(ex, "GetManager failed with status {StatusCode}.", ex.StatusCode);

            var errorResponse = request.CreateResponse((HttpStatusCode)ex.StatusCode);
            await errorResponse
                .WriteStringAsync(ex.ResponseBody, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Gets the direct reports of a specified user using the generated <see cref="Office365usersClient"/>.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="userUPN">The user principal name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("GetDirectReports")]
    public async Task<HttpResponseData> GetDirectReportsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "users/{userUPN}/reports")] HttpRequestData request,
        string userUPN,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("GetDirectReports: Looking up reports for {UserUPN}.", userUPN);

        try
        {
            var reports = await this._office365UsersClient
                .DirectReportsAsync(userUPN: userUPN, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(reports, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (Office365usersConnectorException ex)
        {
            this._logger.LogError(ex, "GetDirectReports failed with status {StatusCode}.", ex.StatusCode);

            var errorResponse = request.CreateResponse((HttpStatusCode)ex.StatusCode);
            await errorResponse
                .WriteStringAsync(ex.ResponseBody, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Searches for users by search term using the generated <see cref="Office365usersClient"/>.
    /// </summary>
    /// <param name="request">The HTTP request. Accepts optional query parameter <c>q</c> for the search term.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("SearchUsers")]
    public async Task<HttpResponseData> SearchUsersAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "users/search")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var searchTerm = request.Query["q"] ?? string.Empty;
        this._logger.LogInformation("SearchUsers: Searching for '{SearchTerm}'.", searchTerm);

        try
        {
            var pageable = this._office365UsersClient.SearchUserAsync(searchTerm: searchTerm);

            // Get just the first page of results
            var firstPage = await pageable
                .GetFirstPageAsync(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(firstPage, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (Office365usersConnectorException ex)
        {
            this._logger.LogError(ex, "SearchUsers failed with status {StatusCode}.", ex.StatusCode);

            var errorResponse = request.CreateResponse((HttpStatusCode)ex.StatusCode);
            await errorResponse
                .WriteStringAsync(ex.ResponseBody, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }
}
