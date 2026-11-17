//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Connectors.Sdk.ServiceNow;
using Azure.Core.Pipeline;
using Moq;
using Moq.Protected;

namespace DirectConnector.Tests;

[TestClass]
public class ServiceNowFunctionsTests
{
    private const string IncidentSysId = "1c741bd70b2322007518478d83673af3";
    private const string AttachmentSysId = "a83820b58f723300e7e16c7827bdeed2";

    private static ServiceNowClient CreateTransportClient(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> onRequest)
    {
        var (credential, options) = TestHelpers.CreateMockedClientSetup(responseFactory:
            () => throw new InvalidOperationException(message: "Unexpected fallback transport request."));
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                methodOrPropertyName: "SendAsync",
                args: new object[]
                {
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>(),
                })
            .Returns((HttpRequestMessage request, CancellationToken _) => onRequest(request));
        options.Transport = new HttpClientTransport(new HttpClient(handler.Object));

        return new ServiceNowClient(
            connectionRuntimeUrl: new Uri(uriString: "https://test.azure.com/connection"),
            credential: credential,
            options: options);
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage
        {
            StatusCode = statusCode,
            Content = new StringContent(content: json, encoding: Encoding.UTF8, mediaType: "application/json"),
        };
    }

    private static IEnumerable<(string Name, string? FileName, string? ContentType, string Content)> ParseMultipartParts(string body, string boundary)
    {
        foreach (var section in body.Split($"--{boundary}", StringSplitOptions.None))
        {
            var separator = section.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (separator < 0)
            {
                continue;
            }

            var headers = section[..separator].Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            var disposition = headers.Single(header => header.StartsWith("Content-Disposition:", StringComparison.OrdinalIgnoreCase));
            var contentType = headers.SingleOrDefault(header => header.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase));
            yield return (
                Regex.Match(disposition, "\\bname=\"?([^\";]+)", RegexOptions.None, TimeSpan.FromSeconds(1)).Groups[1].Value,
                Regex.Match(disposition, "\\bfilename=\"([^\"]*)\"", RegexOptions.None, TimeSpan.FromSeconds(1)) is { Success: true } fileName ? fileName.Groups[1].Value : null,
                contentType?["Content-Type:".Length..].Split(';')[0].Trim(),
                section[(separator + 4)..^2]);
        }
    }

    [TestMethod]
    public async Task ServiceNowAttachmentRoundTripAsync_WithBody_UploadsMultipartAttachmentAndCleansUp()
    {
        // Arrange — sanitized payloads based on real ServiceNow Table and Attachment API responses
        var calls = new List<string>();
        var uploadedParts = new Dictionary<string, (string? FileName, string? ContentType, byte[] Content)>(StringComparer.Ordinal);
        byte[] storedContent = Array.Empty<byte>();

        using var client = CreateTransportClient(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            calls.Add($"{request.Method} {path}");

            if (request.Method == HttpMethod.Post && path.EndsWith("/api/now/v2/table/incident", StringComparison.Ordinal))
            {
                StringAssert.Contains(request.RequestUri.Query, "sysparm_fields=sys_id%2Cnumber");
                var record = await request.Content!.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false);
                StringAssert.Contains(record, "\"short_description\"");
                return Json($$$"""{"result":{"sys_id":"{{{IncidentSysId}}}","number":"INC0010001"}}""");
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/api/now/v1/attachment/upload", StringComparison.Ordinal))
            {
                Assert.AreEqual("multipart/form-data", request.Content!.Headers.ContentType?.MediaType);
                var boundary = request.Content.Headers.ContentType!.Parameters.Single(parameter => parameter.Name == "boundary").Value!.Trim('"');
                var wireBody = await request.Content.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false);
                foreach (var part in ParseMultipartParts(wireBody, boundary))
                {
                    uploadedParts[part.Name] = (part.FileName, part.ContentType, Encoding.UTF8.GetBytes(part.Content));
                }

                storedContent = uploadedParts["attachment_content"].Content;
                return Json($$$"""{"result":{"sys_id":"{{{AttachmentSysId}}}","file_name":"notes.txt","content_type":"text/plain"}}""", HttpStatusCode.Created);
            }

            if (request.Method == HttpMethod.Get && path.EndsWith($"/api/now/v1/attachment/{AttachmentSysId}/file", StringComparison.Ordinal))
            {
                return new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new ByteArrayContent(storedContent) };
            }

            if (request.Method == HttpMethod.Delete)
            {
                return new HttpResponseMessage { StatusCode = HttpStatusCode.NoContent };
            }

            throw new InvalidOperationException(message: $"Unexpected request {request.Method} {path}.");
        });

        var functions = new ServiceNowFunctions(TestHelpers.CreateNullLogger<ServiceNowFunctions>(), client);
        var request = TestHelpers.CreateRequest(
            body: "Hello from the Connectors SDK.",
            method: "POST",
            url: "https://localhost/api/servicenow/attachments/roundtrip?fileName=notes.txt");

        // Act
        var response = await functions.ServiceNowAttachmentRoundTripAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        // Assert
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, body);
        StringAssert.Contains(body, "\"contentMatches\":true");
        StringAssert.Contains(body, "\"incidentNumber\":\"INC0010001\"");
        CollectionAssert.AreEqual(
            new[]
            {
                "POST /connection/api/now/v2/table/incident",
                "POST /connection/api/now/v1/attachment/upload",
                $"GET /connection/api/now/v1/attachment/{AttachmentSysId}/file",
                $"DELETE /connection/api/now/v1/attachment/{AttachmentSysId}",
                $"DELETE /connection/api/now/v2/table/incident/{IncidentSysId}",
            },
            calls);
        Assert.AreEqual("incident", Encoding.UTF8.GetString(uploadedParts["table_name"].Content));
        Assert.AreEqual(IncidentSysId, Encoding.UTF8.GetString(uploadedParts["table_sys_id"].Content));
        Assert.AreEqual("notes.txt", uploadedParts["attachment_content"].FileName);
        Assert.AreEqual("text/plain", uploadedParts["attachment_content"].ContentType);
        Assert.AreEqual("Hello from the Connectors SDK.", Encoding.UTF8.GetString(uploadedParts["attachment_content"].Content));
    }

    [TestMethod]
    public async Task ServiceNowAttachmentRoundTripAsync_WhenUploadFails_DeletesIncidentAndReturns502()
    {
        // Arrange
        var calls = new List<string>();
        using var client = CreateTransportClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            calls.Add($"{request.Method} {path}");

            return Task.FromResult(request.Method == HttpMethod.Post && path.EndsWith("/table/incident", StringComparison.Ordinal)
                ? Json($$$"""{"result":{"sys_id":"{{{IncidentSysId}}}","number":"INC0010002"}}""")
                : request.Method == HttpMethod.Delete
                    ? new HttpResponseMessage { StatusCode = HttpStatusCode.NoContent }
                    : Json("""{"error":{"message":"Invalid file type"}}""", HttpStatusCode.BadRequest));
        });

        var functions = new ServiceNowFunctions(TestHelpers.CreateNullLogger<ServiceNowFunctions>(), client);
        var request = TestHelpers.CreateRequest(method: "POST", url: "https://localhost/api/servicenow/attachments/roundtrip");

        // Act
        var response = await functions.ServiceNowAttachmentRoundTripAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadGateway, response.StatusCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "POST /connection/api/now/v2/table/incident",
                "POST /connection/api/now/v1/attachment/upload",
                $"DELETE /connection/api/now/v2/table/incident/{IncidentSysId}",
            },
            calls);
    }

    [TestMethod]
    public async Task ServiceNowAttachmentRoundTripAsync_WhenCreateResponseFails_DeletesIncidentFoundByDescription()
    {
        // Arrange — the create call fails after ServiceNow may already have inserted the incident
        var calls = new List<string>();
        string? sentDescription = null;
        string? cleanupQuery = null;
        using var client = CreateTransportClient(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            calls.Add($"{request.Method} {path}");

            if (request.Method == HttpMethod.Post)
            {
                var record = await request.Content!.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false);
                sentDescription = JsonDocument.Parse(record).RootElement.GetProperty("short_description").GetString();
                return Json("""{"error":{"message":"Gateway timeout"}}""", HttpStatusCode.GatewayTimeout);
            }

            if (request.Method == HttpMethod.Get)
            {
                cleanupQuery = Uri.UnescapeDataString(request.RequestUri.Query);
                return Json($$$"""{"result":[{"sys_id":"{{{IncidentSysId}}}"}]}""");
            }

            return new HttpResponseMessage { StatusCode = HttpStatusCode.NoContent };
        });

        var functions = new ServiceNowFunctions(TestHelpers.CreateNullLogger<ServiceNowFunctions>(), client);
        var request = TestHelpers.CreateRequest(method: "POST", url: "https://localhost/api/servicenow/attachments/roundtrip");

        // Act
        var response = await functions.ServiceNowAttachmentRoundTripAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.IsNotNull(sentDescription);
        StringAssert.Contains(cleanupQuery, $"sysparm_query=short_description={sentDescription}");
        CollectionAssert.AreEqual(
            new[]
            {
                "POST /connection/api/now/v2/table/incident",
                "GET /connection/api/now/v2/table/incident",
                $"DELETE /connection/api/now/v2/table/incident/{IncidentSysId}",
            },
            calls);
    }
}
