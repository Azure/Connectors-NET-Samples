//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using Microsoft.Azure.Connectors.Sdk.Azureblob;
using Microsoft.Azure.Connectors.Sdk;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DirectConnector;

/// <summary>
/// Azure Functions demonstrating Azure Blob Storage operations using the generated
/// <see cref="AzureblobClient"/> from the Connectors SDK.
/// </summary>
/// <remarks>
/// Azure Blob Storage uses key-based auth (accountName + accessKey), not OAuth.
/// The connection must be created with parameterValues in a single PUT — no consent link flow.
/// </remarks>
public class AzureBlobFunctions
{
    private readonly ILogger<AzureBlobFunctions> _logger;
    private readonly AzureblobClient _azureBlobClient;

    public AzureBlobFunctions(
        ILogger<AzureBlobFunctions> logger,
        AzureblobClient azureBlobClient)
    {
        this._logger = logger;
        this._azureBlobClient = azureBlobClient;
    }

    /// <summary>
    /// Gets blob metadata using the generated <see cref="AzureblobClient"/>.
    /// Exercises the <see cref="DataWithSensitivityLabelInfo"/> response type.
    /// </summary>
    [Function("GetBlobMetadata")]
    public async Task<HttpResponseData> GetBlobMetadataAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "blob/metadata")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("GetBlobMetadata: Using generated AzureblobClient.");

        var storageAccount = request.Query["account"];
        var blobPath = request.Query["path"];
        if (string.IsNullOrWhiteSpace(storageAccount) || string.IsNullOrWhiteSpace(blobPath))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { error = "Query parameters 'account' and 'path' are required." })
                .ConfigureAwait(continueOnCapturedContext: false);
            return badRequest;
        }

        try
        {
            var metadata = await this._azureBlobClient
                .GetFileMetadataByPathAsync(
                    storageAccountNameOrBlobEndpoint: storageAccount,
                    blobPath: blobPath,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            this._logger.LogInformation("Got metadata for blob '{BlobName}': '{Size}' bytes.", metadata.Name, metadata.Size);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(metadata)
                .ConfigureAwait(continueOnCapturedContext: false);
            return response;
        }
        catch (AzureblobConnectorException ex)
        {
            this._logger.LogError(ex, "Azure Blob connector error: '{StatusCode}'.", ex.StatusCode);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.StatusCode, details = ex.ResponseBody })
                .ConfigureAwait(continueOnCapturedContext: false);
            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in GetBlobMetadata.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);
            return errorResponse;
        }
    }

    /// <summary>
    /// Downloads blob content using the generated <see cref="AzureblobClient"/>.
    /// Exercises the byte[] return path.
    /// </summary>
    [Function("DownloadBlob")]
    public async Task<HttpResponseData> DownloadBlobAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "blob/download")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("DownloadBlob: Using generated AzureblobClient byte[] response path.");

        var storageAccount = request.Query["account"];
        var blobPath = request.Query["path"];
        if (string.IsNullOrWhiteSpace(storageAccount) || string.IsNullOrWhiteSpace(blobPath))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { error = "Query parameters 'account' and 'path' are required." })
                .ConfigureAwait(continueOnCapturedContext: false);
            return badRequest;
        }

        try
        {
            var fileBytes = await this._azureBlobClient
                .GetFileContentByPathAsync(
                    storageAccountNameOrBlobEndpoint: storageAccount,
                    blobPath: blobPath,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var fileName = System.IO.Path.GetFileName(blobPath)
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty)
                .Replace("\"", string.Empty);

            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/octet-stream");
            response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");
            await response.Body
                .WriteAsync(fileBytes, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            this._logger.LogInformation("Downloaded blob '{FileName}': '{ByteCount}' bytes.", fileName, fileBytes.Length);

            return response;
        }
        catch (AzureblobConnectorException ex)
        {
            this._logger.LogError(ex, "Azure Blob connector error: '{StatusCode}'.", ex.StatusCode);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.StatusCode, details = ex.ResponseBody })
                .ConfigureAwait(continueOnCapturedContext: false);
            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in DownloadBlob.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);
            return errorResponse;
        }
    }

    /// <summary>
    /// Uploads a blob using the generated <see cref="AzureblobClient"/>.
    /// Exercises the byte[] input path with <see cref="BlobMetadata"/> response.
    /// </summary>
    [Function("UploadBlob")]
    public async Task<HttpResponseData> UploadBlobAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "blob/upload")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("UploadBlob: Using generated AzureblobClient.");

        var storageAccount = request.Query["account"];
        var folder = request.Query["folder"];
        var blobName = request.Query["name"];
        if (string.IsNullOrWhiteSpace(storageAccount) || string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(blobName))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { error = "Query parameters 'account', 'folder', and 'name' are required." })
                .ConfigureAwait(continueOnCapturedContext: false);
            return badRequest;
        }

        try
        {
            using var memoryStream = new MemoryStream();
            await request.Body
                .CopyToAsync(memoryStream, cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
            var bodyBytes = memoryStream.ToArray();

            var metadata = await this._azureBlobClient
                .CreateFileAsync(
                    storageAccountNameOrBlobEndpoint: storageAccount,
                    input: bodyBytes,
                    folderPath: folder,
                    blobName: blobName,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            this._logger.LogInformation("Uploaded blob '{BlobName}' to '{Folder}': '{Size}' bytes.", blobName, folder, bodyBytes.Length);

            var response = request.CreateResponse(HttpStatusCode.Created);
            await response
                .WriteAsJsonAsync(metadata)
                .ConfigureAwait(continueOnCapturedContext: false);
            return response;
        }
        catch (AzureblobConnectorException ex)
        {
            this._logger.LogError(ex, "Azure Blob connector error: '{StatusCode}'.", ex.StatusCode);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.StatusCode, details = ex.ResponseBody })
                .ConfigureAwait(continueOnCapturedContext: false);
            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in UploadBlob.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);
            return errorResponse;
        }
    }

    /// <summary>
    /// Deletes a blob using the generated <see cref="AzureblobClient"/>.
    /// Exercises the void (no response body) path.
    /// </summary>
    [Function("DeleteBlob")]
    public async Task<HttpResponseData> DeleteBlobAsync(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "blob/delete")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("DeleteBlob: Using generated AzureblobClient.");

        var storageAccount = request.Query["account"];
        var blobId = request.Query["id"];
        if (string.IsNullOrWhiteSpace(storageAccount) || string.IsNullOrWhiteSpace(blobId))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest
                .WriteAsJsonAsync(new { error = "Query parameters 'account' and 'id' are required." })
                .ConfigureAwait(continueOnCapturedContext: false);
            return badRequest;
        }

        try
        {
            await this._azureBlobClient
                .DeleteFileAsync(
                    storageAccountNameOrBlobEndpoint: storageAccount,
                    blob: blobId,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            this._logger.LogInformation("Deleted blob '{BlobId}'.", blobId);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response
                .WriteAsJsonAsync(new { success = true, deleted = blobId })
                .ConfigureAwait(continueOnCapturedContext: false);
            return response;
        }
        catch (AzureblobConnectorException ex)
        {
            this._logger.LogError(ex, "Azure Blob connector error: '{StatusCode}'.", ex.StatusCode);

            var errorResponse = request.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message, statusCode = ex.StatusCode, details = ex.ResponseBody })
                .ConfigureAwait(continueOnCapturedContext: false);
            return errorResponse;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            this._logger.LogError(ex, "Error in DeleteBlob.");

            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse
                .WriteAsJsonAsync(new { success = false, error = ex.Message })
                .ConfigureAwait(continueOnCapturedContext: false);
            return errorResponse;
        }
    }
}
