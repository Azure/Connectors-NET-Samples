//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Connectors.DirectClient.Mq;
using Microsoft.Azure.Connectors.DirectClient.Office365;
using Microsoft.Azure.Connectors.DirectClient.Sharepointonline;
using Microsoft.Azure.Connectors.DirectClient.Smtp;
using Microsoft.Azure.Connectors.DirectClient.Teams;
using Microsoft.Azure.Connectors.Sdk;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SharePointBlobMetadata = Microsoft.Azure.Connectors.DirectClient.Sharepointonline.BlobMetadata;

namespace DirectConnector;

/// <summary>
/// Azure Functions that use the generated <see cref="Office365Client"/>, <see cref="SharepointonlineClient"/>,
/// and <see cref="TeamsClient"/> from the DirectClient SDK.
/// </summary>
/// <remarks>
/// Demonstrates DI-based lifetime management, JSON deserialization for structured responses,
/// binary content (byte[]) download and upload, connector-specific exception handling,
/// and CancellationToken propagation from the Functions host.
/// </remarks>
public class ConnectorFunctions
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

    /// <summary>
    /// Teams connector API path template for posting messages.
    /// Parameters: {0} = poster (e.g., "Flow bot"), {1} = location (e.g., "Channel").
    /// </summary>
    private const string TeamsPostMessagePathTemplate = "/beta/teams/conversation/message/poster/{0}/location/{1}";

    /// <summary>
    /// Default poster identity for Teams messages posted via the connector.
    /// </summary>
    private const string TeamsDefaultPoster = "Flow bot";

    /// <summary>
    /// Default message location for Teams channel posts.
    /// </summary>
    private const string TeamsDefaultLocation = "Channel";

    private readonly ILogger<ConnectorFunctions> _logger;
    private readonly Office365Client _office365Client;
    private readonly SharepointonlineClient _sharePointClient;
    private readonly SmtpClient _smtpClient;
    private readonly TeamsClient _teamsClient;
    private readonly MqClient _mqClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectorFunctions"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="office365Client">The DI-injected Office365 client (disposed by the host).</param>
    /// <param name="sharePointClient">The DI-injected SharePoint client (disposed by the host).</param>
    /// <param name="smtpClient">The DI-injected SMTP client (disposed by the host).</param>
    /// <param name="teamsClient">The DI-injected Teams client (disposed by the host).</param>
    /// <param name="mqClient">The DI-injected MQ client (disposed by the host).</param>
    public ConnectorFunctions(
        ILogger<ConnectorFunctions> logger,
        Office365Client office365Client,
        SharepointonlineClient sharePointClient,
        SmtpClient smtpClient,
        TeamsClient teamsClient,
        MqClient mqClient)
    {
        this._logger = logger;
        this._office365Client = office365Client;
        this._sharePointClient = sharePointClient;
        this._smtpClient = smtpClient;
        this._teamsClient = teamsClient;
        this._mqClient = mqClient;
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
                input = JsonSerializer.Deserialize<SendEmailRequest>(body, ConnectorFunctions.JsonOptions);
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
        catch (Office365ConnectorException ex)
        {
            this._logger.LogError(ex, "Connector error: '{StatusCode}'.", ex.StatusCode);

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
        catch (Office365ConnectorException ex)
        {
            this._logger.LogError(ex, "Connector error: '{StatusCode}'.", ex.StatusCode);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message,
                    statusCode = ex.StatusCode
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
    /// Gets all SharePoint lists and libraries for a site using the generated <see cref="SharepointonlineClient"/>.
    /// </summary>
    /// <param name="request">The HTTP request containing the site address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("GetSharePointLists")]
    public async Task<HttpResponseData> GetSharePointListsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "sharepoint/lists")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("GetSharePointLists: Using generated SharepointonlineClient from SDK.");

        var siteAddress = request.Query["site"];
        if (string.IsNullOrEmpty(siteAddress))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { error = "Query parameter 'site' is required." })
                .ConfigureAwait(continueOnCapturedContext: false);
            return badRequest;
        }

        try
        {
            var tables = await this._sharePointClient
                .GetAllTablesAsync(siteAddress, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    site = siteAddress,
                    count = tables?.Value?.Count ?? 0,
                    lists = tables?.Value?.Select(table => new
                    {
                        name = table.Name,
                        displayName = table.DisplayName
                    })
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (SharepointonlineConnectorException ex)
        {
            this._logger.LogError(ex, "SharePoint connector error: '{StatusCode}'.", ex.StatusCode);

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
            this._logger.LogError(ex, "Error in GetSharePointLists.");

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
    /// Lists files in a SharePoint folder using the generated <see cref="SharepointonlineClient"/>.
    /// </summary>
    /// <remarks>
    /// Exercises the <see cref="SharePointBlobMetadata"/> model for folder browsing.
    /// </remarks>
    /// <param name="request">The HTTP request containing site and optional folder identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("ListFolder")]
    public async Task<HttpResponseData> ListFolderAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "sharepoint/files")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("ListFolder: Using generated SharepointonlineClient from SDK.");

        var siteAddress = request.Query["site"];
        if (string.IsNullOrEmpty(siteAddress))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { error = "Query parameter 'site' is required." })
                .ConfigureAwait(continueOnCapturedContext: false);
            return badRequest;
        }

        try
        {
            var folderId = request.Query["folder"];

            // NOTE: ListRootFolderAsync vs ListFolderAsync demonstrates
            // two overloads with the same return type but different parameter sets.
            var files = string.IsNullOrEmpty(folderId)
                ? await this._sharePointClient
                    .ListRootFolderAsync(siteAddress, cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false)
                : await this._sharePointClient
                    .ListFolderAsync(siteAddress, folderId, cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    site = siteAddress,
                    folder = string.IsNullOrEmpty(folderId) ? "(root)" : folderId,
                    count = files?.Count ?? 0,
                    files = (files ?? Enumerable.Empty<SharePointBlobMetadata>()).Select(file => new
                    {
                        id = file.Id,
                        name = file.Name,
                        displayName = file.DisplayName,
                        path = file.Path,
                        size = file.Size,
                        mediaType = file.MediaType,
                        isFolder = file.IsFolder
                    })
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (SharepointonlineConnectorException ex)
        {
            this._logger.LogError(ex, "SharePoint connector error: '{StatusCode}'.", ex.StatusCode);

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
            this._logger.LogError(ex, "Error in ListFolder.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Downloads file content from SharePoint as binary bytes.
    /// </summary>
    /// <remarks>
    /// Exercises the <c>byte[]</c> response path in <see cref="SharepointonlineClient.GetFileContentByPathAsync"/>.
    /// The generated <c>CallConnectorAsync</c> detects <c>byte[]</c> as the response type and uses
    /// <c>ReadAsByteArrayAsync</c> instead of JSON deserialization.
    /// </remarks>
    /// <param name="request">The HTTP request containing site address and file path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("DownloadFile")]
    public async Task<HttpResponseData> DownloadFileAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "sharepoint/download")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("DownloadFile: Using generated SharepointonlineClient byte[] response path.");

        var siteAddress = request.Query["site"];
        var filePath = request.Query["path"];
        if (string.IsNullOrEmpty(siteAddress) || string.IsNullOrEmpty(filePath))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { error = "Query parameters 'site' and 'path' are required." })
                .ConfigureAwait(continueOnCapturedContext: false);
            return badRequest;
        }

        try
        {
            // NOTE: This exercises the byte[] return path in the generated client.
            // CallConnectorAsync<byte[]> uses ReadAsByteArrayAsync instead of JSON deserialization.
            var fileBytes = await this._sharePointClient
                .GetFileContentByPathAsync(siteAddress, filePath, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            // NOTE: Sanitize the filename to prevent response header injection.
            // Path.GetFileName strips directory traversal, and we remove CR/LF and quotes
            // that could corrupt the Content-Disposition header value.
            var fileName = System.IO.Path.GetFileName(filePath)
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty)
                .Replace("\"", string.Empty);

            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/octet-stream");
            response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");
            await response.Body
                .WriteAsync(fileBytes, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            this._logger.LogInformation("Downloaded '{FileName}': '{ByteCount}' bytes.", fileName, fileBytes.Length);

            return response;
        }
        catch (SharepointonlineConnectorException ex)
        {
            this._logger.LogError(ex, "SharePoint connector error: '{StatusCode}'.", ex.StatusCode);

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
            this._logger.LogError(ex, "Error in DownloadFile.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Uploads a file to a SharePoint document library.
    /// </summary>
    /// <remarks>
    /// Exercises the <c>byte[]</c> input path in <see cref="SharepointonlineClient.CreateFileAsync"/>.
    /// Accepts a JSON body with base64-encoded content or plain text, and uploads it to
    /// the specified SharePoint folder.
    /// </remarks>
    /// <param name="request">The HTTP request containing upload details.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("UploadFile")]
    public async Task<HttpResponseData> UploadFileAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "sharepoint/upload")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("UploadFile: Using generated SharepointonlineClient byte[] input path.");

        try
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader
                .ReadToEndAsync(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            UploadFileRequest? input;
            try
            {
                input = JsonSerializer.Deserialize<UploadFileRequest>(body, ConnectorFunctions.JsonOptions);
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

            if (input == null ||
                string.IsNullOrEmpty(input.Site) ||
                string.IsNullOrEmpty(input.FolderPath) ||
                string.IsNullOrEmpty(input.FileName))
            {
                var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest
                    .WriteAsJsonAsync(new { error = "Fields 'site', 'folderPath', and 'fileName' are required." })
                    .ConfigureAwait(continueOnCapturedContext: false);
                return badRequest;
            }

            // NOTE: Support both base64-encoded binary and plain text content.
            byte[] fileBytes;
            try
            {
                fileBytes = !string.IsNullOrEmpty(input.ContentBase64)
                    ? Convert.FromBase64String(input.ContentBase64)
                    : Encoding.UTF8.GetBytes(input.Content ?? string.Empty);
            }
            catch (FormatException ex)
            {
                this._logger.LogError(ex, "Invalid base64 content in 'contentBase64'.");

                var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest
                    .WriteAsJsonAsync(new { error = "The 'contentBase64' field must contain valid base64-encoded data." })
                    .ConfigureAwait(continueOnCapturedContext: false);

                return badRequest;
            }

            var metadata = await this._sharePointClient
                .CreateFileAsync(input.Site, fileBytes, input.FolderPath, input.FileName, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    message = $"File '{input.FileName}' uploaded to '{input.FolderPath}'.",
                    fileId = metadata?.Id,
                    name = metadata?.Name,
                    path = metadata?.Path,
                    size = fileBytes.Length
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (SharepointonlineConnectorException ex)
        {
            this._logger.LogError(ex, "SharePoint connector error: '{StatusCode}'.", ex.StatusCode);

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
            this._logger.LogError(ex, "Error in UploadFile.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Exports an email message as raw RFC822 (.eml) bytes.
    /// </summary>
    /// <remarks>
    /// Exercises the <c>byte[]</c> response path in <see cref="Office365Client.ExportEmailAsync"/>.
    /// This is the Office365 counterpart to <see cref="DownloadFileAsync"/> for SharePoint —
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
        catch (Office365ConnectorException ex)
        {
            this._logger.LogError(ex, "Connector error: '{StatusCode}'.", ex.StatusCode);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new
                {
                    success = false,
                    error = ex.Message,
                    statusCode = ex.StatusCode
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

            if (contentLength > ConnectorFunctions.MaxTriggerCallbackBodySize ||
                (contentLength < 0 && request.Body.CanSeek && request.Body.Length > ConnectorFunctions.MaxTriggerCallbackBodySize))
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
            var buffer = new char[ConnectorFunctions.MaxTriggerCallbackBodySize + 1];
            var charsRead = await reader
                .ReadBlockAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            if (charsRead > ConnectorFunctions.MaxTriggerCallbackBodySize)
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
                ConnectorFunctions.JsonOptions);

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
    /// Request model for sending email.
    /// </summary>
    private record SendEmailRequest(string? To, string? Subject, string? Body);

    /// <summary>
    /// Request model for uploading a file to SharePoint.
    /// </summary>
    private record UploadFileRequest(
        string? Site,
        string? FolderPath,
        string? FileName,
        string? Content,
        string? ContentBase64);

    /// <summary>
    /// Request model for posting a Teams message.
    /// </summary>
    private record PostTeamsMessageRequest(string? TeamId, string? ChannelId, string? Message);

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
        catch (TeamsConnectorException ex)
        {
            this._logger.LogError(ex, "Teams connector error: '{StatusCode}'.", ex.StatusCode);

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
                        id = channel.ChannelID,
                        displayName = channel.DisplayName,
                        description = channel.DescriptionOfChannel,
                        membershipType = channel.TheTypeOfTheChannel
                    })
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (TeamsConnectorException ex)
        {
            this._logger.LogError(ex, "Teams connector error: '{StatusCode}'.", ex.StatusCode);

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
        catch (TeamsConnectorException ex)
        {
            this._logger.LogError(ex, "Teams connector error: '{StatusCode}'.", ex.StatusCode);

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
                input = JsonSerializer.Deserialize<PostTeamsMessageRequest>(body, ConnectorFunctions.JsonOptions);
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
            var messageRequest = new Microsoft.Azure.Connectors.DirectClient.Teams.DynamicPostMessageRequest();
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
                    postAs: ConnectorFunctions.TeamsDefaultPoster,
                    postIn: ConnectorFunctions.TeamsDefaultLocation,
                    input: messageRequest,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    message = "Message posted to Teams channel via generated TeamsClient from SDK.",
                    messageId = result?.MessageID,
                    messageLink = result?.MessageLink,
                    timestamp = DateTime.UtcNow
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (TeamsConnectorException ex)
        {
            this._logger.LogError(ex, "Teams connector error: '{StatusCode}'.", ex.StatusCode);

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
                input = JsonSerializer.Deserialize<CreateCalendarEventRequest>(body, ConnectorFunctions.JsonOptions);
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
        catch (Office365ConnectorException ex)
        {
            this._logger.LogError(ex, "Connector error: '{StatusCode}'.", ex.StatusCode);

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

    [Function("SmtpSendEmail")]
    public async Task<HttpResponseData> SmtpSendEmailAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "smtp/email")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("SmtpSendEmail: Using generated SmtpClient from SDK.");

        try
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            var input = JsonSerializer.Deserialize<SmtpSendEmailRequest>(body, ConnectorFunctions.JsonOptions);

            if (input == null || string.IsNullOrEmpty(input.To) || string.IsNullOrEmpty(input.From))
            {
                var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "'from' and 'to' are required." }).ConfigureAwait(continueOnCapturedContext: false);
                return badRequest;
            }

            var email = new Email { From = input.From, To = input.To, Subject = input.Subject ?? "No Subject", Body = input.Body ?? string.Empty };
            await this._smtpClient.SendEmailAsync(input: email, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { success = true, message = "Email sent via SmtpClient.", from = input.From, to = input.To, subject = input.Subject, timestamp = DateTime.UtcNow }).ConfigureAwait(continueOnCapturedContext: false);
            return response;
        }
        catch (SmtpConnectorException ex)
        {
            this._logger.LogError(ex, "SMTP connector error: '{StatusCode}'.", ex.StatusCode);
            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse.WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.StatusCode, details = ex.ResponseBody }).ConfigureAwait(continueOnCapturedContext: false);
            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in SmtpSendEmail.");
            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { success = false, error = ex.Message }).ConfigureAwait(continueOnCapturedContext: false);
            return errorResponse;
        }
    }

    private record SmtpSendEmailRequest(string? From, string? To, string? Subject, string? Body);

    [Function("MqSendMessage")]
    public async Task<HttpResponseData> MqSendMessageAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "mq/send")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("MqSendMessage: Using generated MqClient from SDK.");
        try
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            var input = JsonSerializer.Deserialize<MqSendRequest>(body, ConnectorFunctions.JsonOptions);
            if (input == null || string.IsNullOrEmpty(input.Message))
            {
                var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Request body must contain 'message'." }).ConfigureAwait(continueOnCapturedContext: false);
                return badRequest;
            }

            var result = await this._mqClient.SendAsync(new SendValidDataOptions { Message = input.Message, Queue = input.Queue }, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            var response = request.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { success = true, messageId = result.MessageId, correlationId = result.CorrelationId }).ConfigureAwait(continueOnCapturedContext: false);
            return response;
        }
        catch (MqConnectorException ex)
        {
            this._logger.LogError(ex, "MQ send failed: {StatusCode}", ex.StatusCode);
            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { success = false, error = ex.Message }).ConfigureAwait(continueOnCapturedContext: false);
            return errorResponse;
        }
    }

    [Function("MqBrowseMessage")]
    public async Task<HttpResponseData> MqBrowseMessageAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "mq/browse")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("MqBrowseMessage: Browse message from MQ queue.");
        try
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            var input = JsonSerializer.Deserialize<MqBrowseRequest>(body, ConnectorFunctions.JsonOptions);
            var result = await this._mqClient.ReadAsync(new SingleGetValidOptions { Queue = input?.Queue }, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            var response = request.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result).ConfigureAwait(continueOnCapturedContext: false);
            return response;
        }
        catch (MqConnectorException ex)
        {
            this._logger.LogError(ex, "MQ browse failed: {StatusCode}", ex.StatusCode);
            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { success = false, error = ex.Message }).ConfigureAwait(continueOnCapturedContext: false);
            return errorResponse;
        }
    }

    [Function("MqReceiveMessage")]
    public async Task<HttpResponseData> MqReceiveMessageAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "mq/receive")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("MqReceiveMessage: Destructive get from MQ queue.");
        try
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            var input = JsonSerializer.Deserialize<MqBrowseRequest>(body, ConnectorFunctions.JsonOptions);
            var result = await this._mqClient.ReceiveAsync(new SingleGetValidOptions { Queue = input?.Queue }, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            var response = request.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result).ConfigureAwait(continueOnCapturedContext: false);
            return response;
        }
        catch (MqConnectorException ex)
        {
            this._logger.LogError(ex, "MQ receive failed: {StatusCode}", ex.StatusCode);
            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { success = false, error = ex.Message }).ConfigureAwait(continueOnCapturedContext: false);
            return errorResponse;
        }
    }

    private record MqSendRequest(string? Message, string? Queue);
    private record MqBrowseRequest(string? Queue);
}
