//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.SharePointOnline;
using Azure.Connectors.Sdk.SharePointOnline.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SharePointBlobMetadata = Azure.Connectors.Sdk.SharePointOnline.Models.BlobMetadata;

namespace DirectConnector;

/// <summary>
/// Azure Functions that use the generated <see cref="SharePointOnlineClient"/> from the Azure Connectors SDK.
/// </summary>
/// <remarks>
/// Demonstrates list browsing, folder listing, file download/upload,
/// and CancellationToken propagation from the Functions host.
/// </remarks>
public class SharePointFunctions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<SharePointFunctions> _logger;
    private readonly SharePointOnlineClient _sharePointClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="SharePointFunctions"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="sharePointClient">The DI-injected SharePoint client (disposed by the host).</param>
    public SharePointFunctions(
        ILogger<SharePointFunctions> logger,
        SharePointOnlineClient sharePointClient)
    {
        this._logger = logger;
        this._sharePointClient = sharePointClient;
    }

    /// <summary>
    /// Gets all SharePoint lists and libraries for a site using the generated <see cref="SharePointOnlineClient"/>.
    /// </summary>
    /// <param name="request">The HTTP request containing the site address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [Function("GetSharePointLists")]
    public async Task<HttpResponseData> GetSharePointListsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "sharepoint/lists")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("GetSharePointLists: Using generated SharePointOnlineClient from SDK.");

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
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "SharePoint connector error: '{StatusCode}'.", ex.Status);

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
    /// Lists files in a SharePoint folder using the generated <see cref="SharePointOnlineClient"/>.
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
        this._logger.LogInformation("ListFolder: Using generated SharePointOnlineClient from SDK.");

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
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "SharePoint connector error: '{StatusCode}'.", ex.Status);

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
    /// Exercises the <c>byte[]</c> response path in <see cref="SharePointOnlineClient.GetFileContentByPathAsync"/>.
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
        this._logger.LogInformation("DownloadFile: Using generated SharePointOnlineClient byte[] response path.");

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
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "SharePoint connector error: '{StatusCode}'.", ex.Status);

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
    /// Exercises the <c>byte[]</c> input path in <see cref="SharePointOnlineClient.CreateFileAsync"/>.
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
        this._logger.LogInformation("UploadFile: Using generated SharePointOnlineClient byte[] input path.");

        try
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader
                .ReadToEndAsync(cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            UploadFileRequest? input;
            try
            {
                input = JsonSerializer.Deserialize<UploadFileRequest>(body, SharePointFunctions.JsonOptions);
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
        catch (ConnectorException ex)
        {
            this._logger.LogError(ex, "SharePoint connector error: '{StatusCode}'.", ex.Status);

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
            this._logger.LogError(ex, "Error in UploadFile.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);

            return errorResponse;
        }
    }

    /// <summary>
    /// Request model for uploading a file to SharePoint.
    /// </summary>
    private record UploadFileRequest(
        string? Site,
        string? FolderPath,
        string? FileName,
        string? Content,
        string? ContentBase64);
}
