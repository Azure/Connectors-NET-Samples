//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.Outlook;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating Outlook operations using the generated
/// <see cref="OutlookClient"/> from the Azure Connectors SDK.
/// </summary>
public class OutlookFunctions
{
    private readonly ILogger<OutlookFunctions> _logger;
    private readonly OutlookClient _outlookClient;

    public OutlookFunctions(
        ILogger<OutlookFunctions> logger,
        OutlookClient outlookClient)
    {
        this._logger = logger;
        this._outlookClient = outlookClient;
    }

    /// <summary>
    /// Gets the list of calendars for the authenticated user.
    /// </summary>
    [Function("OutlookGetCalendars")]
    public async Task<HttpResponseData> OutlookGetCalendarsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "outlook/calendars")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("OutlookGetCalendars: Using generated OutlookClient from SDK.");

        try
        {
            var calendars = await this._outlookClient
                .CalendarGetTablesAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, calendars }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "OutlookGetCalendars failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in OutlookGetCalendars.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Gets the list of contact folders for the authenticated user.
    /// </summary>
    [Function("OutlookGetContactFolders")]
    public async Task<HttpResponseData> OutlookGetContactFoldersAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "outlook/contacts")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("OutlookGetContactFolders: Using generated OutlookClient from SDK.");

        try
        {
            var folders = await this._outlookClient
                .ContactGetTablesAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, folders }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "OutlookGetContactFolders failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in OutlookGetContactFolders.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }
}
