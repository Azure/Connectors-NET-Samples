//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.Planner;
using Azure.Connectors.Sdk.Planner.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating read-only Microsoft Planner operations.
/// </summary>
public class PlannerFunctions
{
    private readonly ILogger<PlannerFunctions> _logger;
    private readonly PlannerClient _plannerClient;

    public PlannerFunctions(
        ILogger<PlannerFunctions> logger,
        PlannerClient plannerClient)
    {
        this._logger = logger;
        this._plannerClient = plannerClient;
    }

    /// <summary>
    /// Lists Microsoft 365 groups available to Planner.
    /// </summary>
    [Function("PlannerListGroups")]
    public async Task<HttpResponseData> PlannerListGroupsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "planner/groups")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        try
        {
            var groups = await this._plannerClient
                .ListGroupsAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, groups }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "PlannerListGroups failed with status '{StatusCode}'.", ex.Status);

            var response = request.CreateResponse(HttpStatusCode.BadGateway);
            await response
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in PlannerListGroups.");

            var response = request.CreateResponse(HttpStatusCode.InternalServerError);
            await response
                .WriteAsJsonAsync(new { success = false, error = ex.Message }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
    }

    /// <summary>
    /// Lists tasks assigned to the authenticated user.
    /// </summary>
    [Function("PlannerListMyTasks")]
    public async Task<HttpResponseData> PlannerListMyTasksAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "planner/tasks")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tasks = new List<GetTaskResponseV2>();
            await foreach (var task in this._plannerClient
                .ListMyTasksAsync(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false))
            {
                tasks.Add(task);
            }

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, tasks }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "PlannerListMyTasks failed with status '{StatusCode}'.", ex.Status);

            var response = request.CreateResponse(HttpStatusCode.BadGateway);
            await response
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in PlannerListMyTasks.");

            var response = request.CreateResponse(HttpStatusCode.InternalServerError);
            await response
                .WriteAsJsonAsync(new { success = false, error = ex.Message }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
    }
}