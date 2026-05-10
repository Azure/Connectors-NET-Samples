//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.Office365;
using Azure.Connectors.Sdk.Office365.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions that use the generated <see cref="Office365Client"/> from the Azure Connectors SDK.
/// </summary>
/// <remarks>
/// Demonstrates email operations, calendar events, trigger callbacks,
/// and CancellationToken propagation from the Functions host.
/// </remarks>
public class Office365Functions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Maximum accepted request body size for trigger callbacks (1 MB).
    /// Requests exceeding this size are rejected with 200 OK to avoid Connector Gateway retries.
    /// </summary>
    private const int MaxTriggerCallbackBodySize = 1 * 1024 * 1024;

    private readonly ILogger<Office365Functions> _logger;
    private readonly Office365Client _office365Client;

    /// <summary>
    /// Initializes a new instance of the <see cref="Office365Functions"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="office365Client">The DI-injected Office365 client (disposed by the host).</param>
    public Office365Functions(
        ILogger<Office365Functions> logger,
        Office365Client office365Client)
    {
        this._logger = logger;
        this._office365Client = office365Client;
    }

    /// <summary>
    /// Sends an email using the generated <see cref="Office365Client"/>.
    /// </summary>
    /// <param name="request">The HTTP request containing email details.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("SendEmail")]
    public async Task<HttpResponseData> SendEmailAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "email")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("SendEmail: Using generated Office365Client from SDK.");

        try
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader
                .ReadToEndAsync(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            SendEmailRequest? input;
            try
            {
                input = JsonSerializer.Deserialize<SendEmailRequest>(body, Office365Functions.JsonOptions);
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

            if (input == null || string.IsNullOrEmpty(input.To))
            {
                var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest
                    .WriteAsJsonAsync(new { error = "Invalid request body - 'to' is required." })
                    .ConfigureAwait(continueOnCapturedContext: false);
                return badRequest;
            }

            var emailMessage = new SendEmailInput
            {
                To = input.To,
                Subject = input.Subject ?? "No Subject",
                Body = input.Body ?? string.Empty
            };

            await this._office365Client
                .SendEmailAsync(emailMessage, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    message = "Email sent via generated Office365Client from SDK.",
                    to = input.To,
                    subject = input.Subject,
                    timestamp = DateTime.UtcNow
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "Connector error: '{StatusCode}'.", ex.Status);

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
            this._logger.LogError(ex, "Error in SendEmail.");

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
    /// Gets Outlook categories using the generated <see cref="Office365Client"/>.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("GetCategories")]
    public async Task<HttpResponseData> GetCategoriesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "categories")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("GetCategories: Using generated Office365Client from SDK.");

        try
        {
            var categories = await this._office365Client
                .GetOutlookCategoryNamesAsync(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    count = categories?.Count ?? 0,
                    categories = categories
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "Connector error: '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message,
                    statusCode = ex.Status
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in GetCategories.");

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
    /// Exports an email message as raw RFC822 (.eml) bytes.
    /// </summary>
    /// <remarks>
    /// Exercises the <c>byte[]</c> response path in <see cref="Office365Client.ExportEmailAsync"/>.
    /// This is the Office365 counterpart to <see cref="SharePointFunctions.DownloadFileAsync"/> for SharePoint —
    /// both prove that <c>CallConnectorAsync&lt;byte[]&gt;</c> uses <c>ReadAsByteArrayAsync</c>
    /// instead of JSON deserialization.
    /// </remarks>
    /// <param name="request">The HTTP request containing the message ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("ExportEmail")]
    public async Task<HttpResponseData> ExportEmailAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "email/export")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("ExportEmail: Using generated Office365Client byte[] response path.");

        var messageId = request.Query["messageId"];
        if (string.IsNullOrEmpty(messageId))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { error = "Query parameter 'messageId' is required." })
                .ConfigureAwait(continueOnCapturedContext: false);
            return badRequest;
        }

        try
        {
            // NOTE: This exercises the same byte[] return path as SharePoint's
            // GetFileContentByPathAsync, proving the pattern works across connectors.
            var emailBytes = await this._office365Client
                .ExportEmailAsync(messageId, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "message/rfc822");
            response.Headers.Add("Content-Disposition", "attachment; filename=\"exported-email.eml\"");
            await response.Body
                .WriteAsync(emailBytes, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            this._logger.LogInformation("Exported email '{MessageId}': '{ByteCount}' bytes.", messageId, emailBytes.Length);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "Connector error: '{StatusCode}'.", ex.Status);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message,
                    statusCode = ex.Status
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in ExportEmail.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Receives Connector Gateway trigger callback with raw <c>triggerBody()</c> JSON.
    /// </summary>
    /// <remarks>
    /// The Connector Gateway provisions a hidden Consumption Logic App that polls for trigger events
    /// (e.g., OnNewEmail). When fired, it POSTs <c>@triggerBody()</c> to this callback URL
    /// with a function key via <c>?code=</c> query parameter.
    ///
    /// Unauthenticated requests (missing or invalid function key) are rejected with HTTP 401
    /// by the Functions runtime before this handler runs.
    ///
    /// For authenticated invocations, all exceptions return 200 to prevent Connector Gateway retries.
    /// </remarks>
    /// <param name="request">The HTTP request containing the trigger payload.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("TriggerCallback")]
    [ConnectorTriggerMetadata(
        ConnectorName = ConnectorNames.Office365,
        OperationName = Office365TriggerOperations.OnNewEmail,
        Connection = "Connectors:Office365")]
    public async Task<HttpResponseData> TriggerCallbackAsync(
        // NOTE: Function-level key auth. Connector Gateway includes the key via ?code= query parameter
        // in the callbackUrl configured in the TriggerConfig. Preview uses function key; MI before GA.
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "triggerCallback")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("TriggerCallback: Received Connector Gateway trigger callback.");

        try
        {
            // NOTE: Check Content-Length header first (works for all streams),
            // then fall back to Body.Length for seekable streams.
            long contentLength = -1;
            if (request.Headers.TryGetValues("Content-Length", out var contentLengthHeaderValues) &&
                long.TryParse(contentLengthHeaderValues.FirstOrDefault(), out var parsedLength))
            {
                contentLength = parsedLength;
            }

            if (contentLength > Office365Functions.MaxTriggerCallbackBodySize ||
                (contentLength < 0 && request.Body.CanSeek && request.Body.Length > Office365Functions.MaxTriggerCallbackBodySize))
            {
                this._logger.LogWarning("TriggerCallback: Payload too large. Rejecting.");

                var rejectResponse = request.CreateResponse(HttpStatusCode.OK);
                await rejectResponse
                    .WriteAsJsonAsync(new
                    {
                        success = true,
                        message = "Trigger callback received (payload too large, discarded).",
                        receivedAt = DateTime.UtcNow
                    })
                    .ConfigureAwait(continueOnCapturedContext: false);

                return rejectResponse;
            }

            // NOTE: Read at most (limit + 1) chars so oversized non-seekable
            // payloads without Content-Length can still be detected reliably.
            using var reader = new StreamReader(request.Body);
            var buffer = new char[Office365Functions.MaxTriggerCallbackBodySize + 1];
            var charsRead = await reader
                .ReadBlockAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            if (charsRead > Office365Functions.MaxTriggerCallbackBodySize)
            {
                this._logger.LogWarning("TriggerCallback: Payload too large. Rejecting.");

                var rejectResponse = request.CreateResponse(HttpStatusCode.OK);
                await rejectResponse
                    .WriteAsJsonAsync(new
                    {
                        success = true,
                        message = "Trigger callback received (payload too large, discarded).",
                        receivedAt = DateTime.UtcNow
                    })
                    .ConfigureAwait(continueOnCapturedContext: false);

                return rejectResponse;
            }

            var body = new string(buffer, 0, charsRead);

            // NOTE: Use SDK's per-trigger convenience type for typed deserialization.
            // Office365OnNewEmailTriggerPayload is a subclass of TriggerCallbackPayload<GraphClientReceiveMessage>
            // that provides discoverability — the developer no longer needs to know the inner type.
            var payload = JsonSerializer.Deserialize<Office365OnNewEmailTriggerPayload>(
                body,
                Office365Functions.JsonOptions);

            var emails = payload?.Body?.Value;
            var emailCount = emails?.Count ?? 0;

            this._logger.LogInformation(
                "TriggerCallback: Deserialized '{EmailCount}' email(s) using Office365OnNewEmailTriggerPayload.",
                emailCount);

            // NOTE: Cap per-email logging to avoid unbounded log volume on batch triggers.
            // Log only message IDs (not PII like Subject/From) to reduce accidental exposure.
            if (emails != null)
            {
                foreach (var email in emails.Take(5))
                {
                    this._logger.LogDebug(
                        "TriggerCallback email: Id='{Id}', ReceivedTime='{ReceivedTime}', HasAttachments='{HasAttachments}', Importance='{Importance}'.",
                        email.MessageId,
                        email.ReceivedTime,
                        email.HasAttachment,
                        email.Importance);
                }

                if (emailCount > 5)
                {
                    this._logger.LogDebug("TriggerCallback: '{RemainingCount}' additional email(s) not logged.", emailCount - 5);
                }
            }

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    message = "Trigger callback received (typed deserialization via SDK).",
                    receivedAt = DateTime.UtcNow,
                    emailCount
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (JsonException ex)
        {
            this._logger.LogError(ex, "TriggerCallback: Invalid JSON payload: '{Message}'.", ex.Message);

            var errorResponse = request.CreateResponse(HttpStatusCode.OK);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = true,
                    message = "Trigger callback received (non-JSON payload).",
                    receivedAt = DateTime.UtcNow
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in TriggerCallback.");

            // NOTE: Return 200 even on unexpected errors — Connector Gateway treats any 2xx
            // as "delivered" and we don't want transient failures to cause retries.
            var errorResponse = request.CreateResponse(HttpStatusCode.OK);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = true,
                    message = "Trigger callback received (processing error).",
                    receivedAt = DateTime.UtcNow
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Creates a calendar event using the generated <see cref="Office365Client"/>.
    /// </summary>
    /// <param name="request">The HTTP request containing calendar event details.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("CreateCalendarEvent")]
    public async Task<HttpResponseData> CreateCalendarEventAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "calendar/event")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("CreateCalendarEvent: Using generated Office365Client from SDK.");

        try
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader
                .ReadToEndAsync(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            CreateCalendarEventRequest? input;
            try
            {
                input = JsonSerializer.Deserialize<CreateCalendarEventRequest>(body, Office365Functions.JsonOptions);
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

            if (input == null || string.IsNullOrEmpty(input.Subject) ||
                string.IsNullOrEmpty(input.StartTime) || string.IsNullOrEmpty(input.EndTime))
            {
                var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest
                    .WriteAsJsonAsync(new { error = "Fields 'subject', 'startTime', and 'endTime' are required." })
                    .ConfigureAwait(continueOnCapturedContext: false);
                return badRequest;
            }

            var calendarEvent = new GraphCalendarEventClient
            {
                Subject = input.Subject,
                Body = input.Body ?? string.Empty,
                StartTime = input.StartTime,
                EndTime = input.EndTime,
                TimeZone = input.TimeZone ?? "UTC",
                RequiredAttendees = input.RequiredAttendees
            };

            // NOTE: "Calendar" is the default calendar ID for the signed-in user.
            var calendarId = input.CalendarId ?? "Calendar";

            var result = await this._office365Client
                .CalendarPostItemAsync(calendarId, calendarEvent, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    message = "Calendar event created via generated Office365Client from SDK.",
                    eventId = result?.ICalUId,
                    subject = result?.Subject,
                    start = result?.StartTime,
                    end = result?.EndTime,
                    timestamp = DateTime.UtcNow
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "Connector error: '{StatusCode}'.", ex.Status);

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
            this._logger.LogError(ex, "Error in CreateCalendarEvent.");

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
    /// Request model for sending email.
    /// </summary>
    private record SendEmailRequest(string? To, string? Subject, string? Body);

    /// <summary>
    /// Request model for creating a calendar event.
    /// </summary>
    private record CreateCalendarEventRequest(
        string? CalendarId,
        string? Subject,
        string? Body,
        string? StartTime,
        string? EndTime,
        string? TimeZone,
        string? RequiredAttendees);
}
