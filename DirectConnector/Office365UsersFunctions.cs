//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.Office365users;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating Office 365 Users operations using the generated
/// <see cref="Office365usersClient"/> from the DirectClient SDK.
/// </summary>
public class Office365UsersFunctions
{
    private readonly ILogger<Office365UsersFunctions> _logger;
    private readonly Office365usersClient _office365UsersClient;

    public Office365UsersFunctions(
        ILogger<Office365UsersFunctions> logger,
        Office365usersClient office365UsersClient)
    {
        this._logger = logger;
        this._office365UsersClient = office365UsersClient;
    }

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
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "GetMyProfile failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this._logger.LogError(ex, "Error in GetMyProfile.");

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

    [Function("GetUserProfile")]
    public async Task<HttpResponseData> GetUserProfileAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "users/{userPrincipalName}")] HttpRequestData request,
        string userPrincipalName,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("GetUserProfile: Looking up user {UserPrincipalName}.", userPrincipalName);

        try
        {
            var profile = await this._office365UsersClient
                .UserProfileAsync(userUPN: userPrincipalName, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(profile, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "GetUserProfile failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this._logger.LogError(ex, "Error in GetUserProfile.");

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

    [Function("GetManager")]
    public async Task<HttpResponseData> GetManagerAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "users/{userPrincipalName}/manager")] HttpRequestData request,
        string userPrincipalName,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("GetManager: Looking up manager for {UserPrincipalName}.", userPrincipalName);

        try
        {
            var manager = await this._office365UsersClient
                .ManagerAsync(userUPN: userPrincipalName, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(manager, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "GetManager failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this._logger.LogError(ex, "Error in GetManager.");

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

    [Function("GetDirectReports")]
    public async Task<HttpResponseData> GetDirectReportsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "users/{userPrincipalName}/reports")] HttpRequestData request,
        string userPrincipalName,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("GetDirectReports: Looking up reports for {UserPrincipalName}.", userPrincipalName);

        try
        {
            var reports = await this._office365UsersClient
                .DirectReportsAsync(userUPN: userPrincipalName, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(reports, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "GetDirectReports failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this._logger.LogError(ex, "Error in GetDirectReports.");

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

    [Function("SearchUsers")]
    public async Task<HttpResponseData> SearchUsersAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "users/search")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var searchTerm = request.Query["q"];
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { success = false, error = "Query parameter 'q' is required." }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return badRequest;
        }

        this._logger.LogInformation("SearchUsers: Searching for '{SearchTerm}'.", searchTerm);

        try
        {
            var pageable = this._office365UsersClient.SearchUserAsync(searchTerm: searchTerm);

            var users = new List<User>();
            await foreach (var user in pageable
                .WithCancellation(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false))
            {
                users.Add(user);
            }

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(users, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "SearchUsers failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this._logger.LogError(ex, "Error in SearchUsers.");

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
}
