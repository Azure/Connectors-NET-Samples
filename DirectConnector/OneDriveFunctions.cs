//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Connectors.Sdk.Onedriveforbusiness;
using Microsoft.Azure.Connectors.Sdk;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using OneDriveBlobMetadata = Microsoft.Azure.Connectors.Sdk.Onedriveforbusiness.BlobMetadata;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating OneDrive for Business operations using the generated
/// <see cref="OnedriveforbusinessClient"/> from the DirectClient SDK.
/// </summary>
/// <remarks>
/// Exercises folder listing, file download/upload, search, sharing links,
/// and trigger callbacks (both binary content and metadata variants).
/// </remarks>
public class OneDriveFunctions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Maximum accepted request body size for trigger callbacks (1 MB).
    /// </summary>
    private const int MaxTriggerCallbackBodySize = 1 * 1024 * 1024;

    /// <summary>
    /// File search mode for OneDrive search operations.
    /// </summary>
    private const string OneDriveSearchFileSearchMode = "OneDriveSearch";

    /// <summary>
    /// Allowed values for share link type.
    /// </summary>
    private static readonly HashSet<string> AllowedLinkTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "view",
        "edit"
    };

    private readonly ILogger<OneDriveFunctions> _logger;
    private readonly OnedriveforbusinessClient _oneDriveClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="OneDriveFunctions"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="oneDriveClient">The DI-injected OneDrive for Business client (disposed by the host).</param>
    public OneDriveFunctions(
        ILogger<OneDriveFunctions> logger,
        OnedriveforbusinessClient oneDriveClient)
    {
        this._logger = logger;
        this._oneDriveClient = oneDriveClient;
    }

    /// <summary>
    /// Lists files in the root of the user's OneDrive for Business.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("ListOneDriveRoot")]
    public async Task<HttpResponseData> ListOneDriveRootAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "onedrive/root")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("ListOneDriveRoot: Using generated OnedriveforbusinessClient from SDK.");

        try
        {
            var files = await this._oneDriveClient
                .ListRootFolderAsync(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    count = files?.Count ?? 0,
                    files = (files ?? Enumerable.Empty<OneDriveBlobMetadata>()).Select(file => new
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
        catch (OnedriveforbusinessConnectorException ex)
        {
            this._logger.LogError(ex, "OneDrive connector error: '{StatusCode}'.", ex.StatusCode);

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
            this._logger.LogError(ex, "Error in ListOneDriveRoot.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Lists files in a specific OneDrive folder.
    /// </summary>
    /// <remarks>
    /// Exercises <see cref="OnedriveforbusinessClient.ListFolderAsync"/> which returns an
    /// <see cref="IAsyncEnumerable{T}"/> that automatically follows pagination across all pages.
    /// </remarks>
    /// <param name="request">The HTTP request containing the folder identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("ListOneDriveFolder")]
    public async Task<HttpResponseData> ListOneDriveFolderAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "onedrive/files")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("ListOneDriveFolder: Using generated OnedriveforbusinessClient from SDK.");

        var folderId = request.Query["folder"];
        if (string.IsNullOrEmpty(folderId))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { error = "Query parameter 'folder' is required." })
                .ConfigureAwait(continueOnCapturedContext: false);
            return badRequest;
        }

        try
        {
            // ListFolderAsync returns IAsyncEnumerable<BlobMetadata> that automatically
            // follows NextLink pagination across all pages — no manual loop needed.
            var files = new List<object>();
            await foreach (var file in this._oneDriveClient
                .ListFolderAsync(folderId)
                .WithCancellation(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false))
            {
                files.Add(new
                {
                    id = file.Id,
                    name = file.Name,
                    displayName = file.DisplayName,
                    path = file.Path,
                    size = file.Size,
                    mediaType = file.MediaType,
                    isFolder = file.IsFolder
                });
            }

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    folder = folderId,
                    count = files.Count,
                    files
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (OnedriveforbusinessConnectorException ex)
        {
            this._logger.LogError(ex, "OneDrive connector error: '{StatusCode}'.", ex.StatusCode);

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
            this._logger.LogError(ex, "Error in ListOneDriveFolder.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Downloads a file from OneDrive for Business as binary bytes.
    /// </summary>
    /// <remarks>
    /// Exercises the <c>byte[]</c> response path in <see cref="OnedriveforbusinessClient.GetFileContentByPathAsync"/>.
    /// </remarks>
    /// <param name="request">The HTTP request containing the file path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("DownloadOneDriveFile")]
    public async Task<HttpResponseData> DownloadOneDriveFileAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "onedrive/download")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("DownloadOneDriveFile: Using generated OnedriveforbusinessClient byte[] response path.");

        var filePath = request.Query["path"];
        if (string.IsNullOrEmpty(filePath))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { error = "Query parameter 'path' is required." })
                .ConfigureAwait(continueOnCapturedContext: false);
            return badRequest;
        }

        try
        {
            var fileBytes = await this._oneDriveClient
                .GetFileContentByPathAsync(filePath, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            // NOTE: Sanitize the filename to prevent response header injection.
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
        catch (OnedriveforbusinessConnectorException ex)
        {
            this._logger.LogError(ex, "OneDrive connector error: '{StatusCode}'.", ex.StatusCode);

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
            this._logger.LogError(ex, "Error in DownloadOneDriveFile.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Uploads a file to OneDrive for Business.
    /// </summary>
    /// <remarks>
    /// Exercises the <c>byte[]</c> input path in <see cref="OnedriveforbusinessClient.CreateFileAsync"/>.
    /// </remarks>
    /// <param name="request">The HTTP request containing upload details.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("UploadOneDriveFile")]
    public async Task<HttpResponseData> UploadOneDriveFileAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "onedrive/upload")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("UploadOneDriveFile: Using generated OnedriveforbusinessClient byte[] input path.");

        try
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader
                .ReadToEndAsync(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            UploadOneDriveFileRequest? input;
            try
            {
                input = JsonSerializer.Deserialize<UploadOneDriveFileRequest>(body, OneDriveFunctions.JsonOptions);
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
                string.IsNullOrEmpty(input.FolderPath) ||
                string.IsNullOrEmpty(input.FileName))
            {
                var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest
                    .WriteAsJsonAsync(new { error = "Fields 'folderPath' and 'fileName' are required." })
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

            var metadata = await this._oneDriveClient
                .CreateFileAsync(fileBytes, input.FolderPath, input.FileName, cancellationToken)
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
        catch (OnedriveforbusinessConnectorException ex)
        {
            this._logger.LogError(ex, "OneDrive connector error: '{StatusCode}'.", ex.StatusCode);

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
            this._logger.LogError(ex, "Error in UploadOneDriveFile.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Searches for files in a OneDrive folder matching a query string.
    /// </summary>
    /// <param name="request">The HTTP request containing query and optional folder path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("SearchOneDriveFiles")]
    public async Task<HttpResponseData> SearchOneDriveFilesAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "onedrive/search")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("SearchOneDriveFiles: Using generated OnedriveforbusinessClient from SDK.");

        var query = request.Query["query"];
        if (string.IsNullOrEmpty(query))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { error = "Query parameter 'query' is required." })
                .ConfigureAwait(continueOnCapturedContext: false);
            return badRequest;
        }

        try
        {
            var folderPath = request.Query["folder"];

            var files = await this._oneDriveClient
                .FindFilesByPathAsync(
                    searchQuery: query,
                    folderPath: folderPath ?? "/",
                    fileSearchMode: OneDriveFunctions.OneDriveSearchFileSearchMode,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    query,
                    folder = folderPath ?? "/",
                    count = files?.Count ?? 0,
                    files = (files ?? Enumerable.Empty<OneDriveBlobMetadata>()).Select(file => new
                    {
                        id = file.Id,
                        name = file.Name,
                        path = file.Path,
                        size = file.Size,
                        mediaType = file.MediaType,
                        isFolder = file.IsFolder
                    })
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (OnedriveforbusinessConnectorException ex)
        {
            this._logger.LogError(ex, "OneDrive connector error: '{StatusCode}'.", ex.StatusCode);

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
            this._logger.LogError(ex, "Error in SearchOneDriveFiles.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Creates a sharing link for a OneDrive file.
    /// </summary>
    /// <param name="request">The HTTP request containing file ID, link type, and optional scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("CreateOneDriveShareLink")]
    public async Task<HttpResponseData> CreateOneDriveShareLinkAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "onedrive/share")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("CreateOneDriveShareLink: Using generated OnedriveforbusinessClient from SDK.");

        try
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader
                .ReadToEndAsync(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            CreateShareLinkRequest? input;
            try
            {
                input = JsonSerializer.Deserialize<CreateShareLinkRequest>(body, OneDriveFunctions.JsonOptions);
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

            if (input == null || string.IsNullOrEmpty(input.FileId) || string.IsNullOrEmpty(input.LinkType))
            {
                var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest
                    .WriteAsJsonAsync(new { error = "Fields 'fileId' and 'linkType' are required. linkType: 'view' or 'edit'." })
                    .ConfigureAwait(continueOnCapturedContext: false);
                return badRequest;
            }

            if (!OneDriveFunctions.AllowedLinkTypes.Contains(input.LinkType))
            {
                var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest
                    .WriteAsJsonAsync(new { error = $"linkType '{input.LinkType}' is not supported. Allowed values: 'view', 'edit'." })
                    .ConfigureAwait(continueOnCapturedContext: false);
                return badRequest;
            }

            var sharingLink = await this._oneDriveClient
                .CreateShareLinkAsync(input.FileId, input.LinkType, input.LinkScope, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    message = $"Share link created for file '{input.FileId}'.",
                    webUrl = sharingLink?.WebURL
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (OnedriveforbusinessConnectorException ex)
        {
            this._logger.LogError(ex, "OneDrive connector error: '{StatusCode}'.", ex.StatusCode);

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
            this._logger.LogError(ex, "Error in CreateOneDriveShareLink.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Receives Connector Gateway trigger callback for OneDrive for Business file events.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Connector Gateway wraps all trigger payloads in a JSON envelope with a <c>body</c> field.
    /// Both binary and metadata triggers arrive with <c>Content-Type: application/json</c>.
    /// The handler parses the JSON and inspects the <c>body</c> field type to determine the variant:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <term>String body (OnNewFileV2 / OnUpdatedFileV2)</term>
    /// <description>
    /// The <c>body</c> field is a base64-encoded string containing the file bytes.
    /// </description>
    /// </item>
    /// <item>
    /// <term>Object body (OnNewFilesV2 / OnUpdatedFilesV2)</term>
    /// <description>
    /// The <c>body</c> field is a <c>{"value":[...]}</c> object with <see cref="OneDriveBlobMetadata"/> items.
    /// The payload deserializes to <see cref="OnedriveforbusinessOnNewFilesTriggerPayload"/>.
    /// </description>
    /// </item>
    /// </list>
    /// </remarks>
    /// <param name="request">The HTTP request containing the trigger payload.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("OneDriveTriggerCallback")]
    [ConnectorTriggerMetadata(
        ConnectorName = ConnectorNames.Onedriveforbusiness,
        OperationName = OnedriveforbusinessTriggerOperations.OnNewFiles,
        Connection = "Connectors:OneDrive")]
    public async Task<HttpResponseData> OneDriveTriggerCallbackAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "onedriveTriggerCallback")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("OneDriveTriggerCallback: Received Connector Gateway trigger callback.");

        try
        {
            // NOTE: Enforce size limit in bytes using a bounded byte read.
            // This avoids the char-vs-byte mismatch that char[] buffers have with non-ASCII.
            var boundedBuffer = new byte[OneDriveFunctions.MaxTriggerCallbackBodySize + 1];
            int totalBytesRead = 0;
            int bytesRead;
            while (totalBytesRead < boundedBuffer.Length &&
                (bytesRead = await request.Body
                    .ReadAsync(boundedBuffer.AsMemory(totalBytesRead, boundedBuffer.Length - totalBytesRead), cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false)) > 0)
            {
                totalBytesRead += bytesRead;
            }

            if (totalBytesRead > OneDriveFunctions.MaxTriggerCallbackBodySize)
            {
                this._logger.LogWarning("OneDriveTriggerCallback: Payload too large ({ByteCount} bytes). Rejecting.", totalBytesRead);

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

            // NOTE: Parse as JSON regardless of Content-Type. Both binary and metadata
            // triggers arrive as JSON with Content-Type: application/json. We inspect the
            // "body" field type to determine the variant.
            var body = Encoding.UTF8.GetString(boundedBuffer, 0, totalBytesRead);

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.TryGetProperty("body", out var bodyElement) &&
                bodyElement.ValueKind == JsonValueKind.String)
            {
                // NOTE: OnNewFileV2 (binary content trigger).
                // The "body" field is a base64-encoded string containing the file bytes.
                var base64Content = bodyElement.GetString() ?? string.Empty;

                // NOTE: The base64 string may be wrapped in extra quotes from
                // the Logic Apps expression engine. Strip them.
                base64Content = base64Content.Trim('"');

                if (string.IsNullOrWhiteSpace(base64Content))
                {
                    this._logger.LogWarning("OneDriveTriggerCallback: Empty base64 body field.");

                    var emptyResponse = request.CreateResponse(HttpStatusCode.OK);
                    await emptyResponse
                        .WriteAsJsonAsync(new
                        {
                            success = true,
                            message = "Trigger callback received (empty body).",
                            receivedAt = DateTime.UtcNow
                        })
                        .ConfigureAwait(continueOnCapturedContext: false);

                    return emptyResponse;
                }

                var maximumDecodedLength = (base64Content.Length / 4) * 3;
                var fileBytesBuffer = new byte[maximumDecodedLength];

                if (!Convert.TryFromBase64String(base64Content, fileBytesBuffer, out var decodedByteCount))
                {
                    this._logger.LogWarning(
                        "OneDriveTriggerCallback: body field is a string but not valid base64 ({Length} chars).",
                        base64Content.Length);

                    var fallbackResponse = request.CreateResponse(HttpStatusCode.OK);
                    await fallbackResponse
                        .WriteAsJsonAsync(new
                        {
                            success = true,
                            message = "Trigger callback received (non-base64 string body).",
                            receivedAt = DateTime.UtcNow,
                            triggerType = "file-content",
                            bodyLength = base64Content.Length
                        })
                        .ConfigureAwait(continueOnCapturedContext: false);

                    return fallbackResponse;
                }

                this._logger.LogInformation(
                    "OneDriveTriggerCallback: Decoded '{ByteCount}' bytes from OnNewFileV2 binary trigger (base64 in JSON envelope).",
                    decodedByteCount);

                var binaryResponse = request.CreateResponse(HttpStatusCode.OK);
                await binaryResponse
                    .WriteAsJsonAsync(new
                    {
                        success = true,
                        message = "Trigger callback received (binary file content via base64 in JSON).",
                        receivedAt = DateTime.UtcNow,
                        triggerType = "file-content",
                        byteCount = decodedByteCount
                    })
                    .ConfigureAwait(continueOnCapturedContext: false);

                return binaryResponse;
            }

            // NOTE: OnNewFilesV2 / OnUpdatedFilesV2 (properties-only trigger).
            // The "body" field is a {"value":[...]} object with BlobMetadata items.
            var payload = JsonSerializer.Deserialize<OnedriveforbusinessOnNewFilesTriggerPayload>(
                body,
                OneDriveFunctions.JsonOptions);

            var files = payload?.Body?.Value;
            var fileCount = files?.Count ?? 0;

            this._logger.LogInformation(
                "OneDriveTriggerCallback: Deserialized '{FileCount}' file(s) from properties-only trigger.",
                fileCount);

            if (files != null)
            {
                foreach (var file in files.Take(5))
                {
                    this._logger.LogDebug(
                        "OneDriveTriggerCallback file: Id='{Id}', Name='{Name}', Path='{Path}', Size='{Size}'.",
                        file.Id,
                        file.Name,
                        file.Path,
                        file.Size);
                }

                if (fileCount > 5)
                {
                    this._logger.LogDebug("OneDriveTriggerCallback: '{RemainingCount}' additional file(s) not logged.", fileCount - 5);
                }
            }

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new
                {
                    success = true,
                    message = "Trigger callback received (JSON metadata via SDK).",
                    receivedAt = DateTime.UtcNow,
                    triggerType = "properties-only",
                    fileCount
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            return response;
        }
        catch (JsonException ex)
        {
            this._logger.LogError(ex, "OneDriveTriggerCallback: Invalid JSON payload: '{Message}'.", ex.Message);

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
            this._logger.LogError(ex, "Error in OneDriveTriggerCallback.");

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
    /// Request model for uploading a file to OneDrive for Business.
    /// </summary>
    private record UploadOneDriveFileRequest(
        string? FolderPath,
        string? FileName,
        string? Content,
        string? ContentBase64);

    /// <summary>
    /// Request model for creating a share link for a OneDrive file.
    /// </summary>
    private record CreateShareLinkRequest(string? FileId, string? LinkType, string? LinkScope);
}
