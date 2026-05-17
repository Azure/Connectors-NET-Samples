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
/// Azure Functions demonstrating Outlook connector operations using the generated
/// <see cref="OutlookClient"/> from the Azure Connectors SDK.
/// </summary>
/// <remarks>
/// The Outlook connector provides calendar, contact, and email operations.
/// For the Office 365 mail connector (different from Outlook.com),
/// see <see cref="Office365Functions"/>.
/// </remarks>
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
    /// Lists available calendars via the Outlook connector.
    /// </summary>
    [Function("OutlookListCalendars")]
    public async Task<HttpResponseData> OutlookListCalendarsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "outlook/calendars")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("OutlookListCalendars: Using generated OutlookClient from SDK.");

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
            this._logger.LogError(ex, "OutlookListCalendars failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in OutlookListCalendars.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Lists available contact folders via the Outlook connector.
    /// </summary>
    [Function("OutlookListContactFolders")]
    public async Task<HttpResponseData> OutlookListContactFoldersAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "outlook/contactfolders")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("OutlookListContactFolders: Listing contact folders via OutlookClient.");

        try
        {
            var contactFolders = await this._outlookClient
                .ContactGetTablesAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, contactFolders }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "OutlookListContactFolders failed with status '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.Status, details = ex.ResponseBody }, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in OutlookListContactFolders.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }
}
