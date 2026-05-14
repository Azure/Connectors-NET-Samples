//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.ExcelOnlineBusiness;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating Excel Online (Business) operations using the generated
/// <see cref="ExcelOnlineBusinessClient"/> from the Azure Connectors SDK.
/// </summary>
public class ExcelOnlineBusinessFunctions
{
    private readonly ILogger<ExcelOnlineBusinessFunctions> _logger;
    private readonly ExcelOnlineBusinessClient _excelOnlineBusinessClient;

    public ExcelOnlineBusinessFunctions(
        ILogger<ExcelOnlineBusinessFunctions> logger,
        ExcelOnlineBusinessClient excelOnlineBusinessClient)
    {
        this._logger = logger;
        this._excelOnlineBusinessClient = excelOnlineBusinessClient;
    }

    /// <summary>
    /// Gets the list of sources (drives/sites) accessible via the connection.
    /// </summary>
    [Function("ExcelOnlineBusinessGetSources")]
    public async Task<HttpResponseData> ExcelOnlineBusinessGetSourcesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "excelonlinebusiness/sources")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("ExcelOnlineBusinessGetSources: Using generated ExcelOnlineBusinessClient from SDK.");

        try
        {
            var sources = await this._excelOnlineBusinessClient
                .GetSourcesAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, sources }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "ExcelOnlineBusinessGetSources failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in ExcelOnlineBusinessGetSources.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Gets the list of drives for the specified location (source).
    /// Route: GET /excelonlinebusiness/drives?location={location}
    /// </summary>
    [Function("ExcelOnlineBusinessGetDrives")]
    public async Task<HttpResponseData> ExcelOnlineBusinessGetDrivesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "excelonlinebusiness/drives")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("ExcelOnlineBusinessGetDrives: Using generated ExcelOnlineBusinessClient from SDK.");

        var location = System.Web.HttpUtility.ParseQueryString(request.Url.Query)["location"];
        if (string.IsNullOrWhiteSpace(location))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { success = false, error = "Missing required query parameter 'location'." }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return badRequest;
        }

        try
        {
            var drives = await this._excelOnlineBusinessClient
                .GetDrivesAsync(location: location, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, drives }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "ExcelOnlineBusinessGetDrives failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in ExcelOnlineBusinessGetDrives.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }
}
