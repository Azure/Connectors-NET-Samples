//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Connectors.Sdk.WordOnlineBusiness;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker.Http;
using Moq;
using Moq.Protected;

namespace DirectConnector.Tests;

[TestClass]
public class WordOnlineBusinessFunctionsTests
{
    private const string Selectors = "source=me&drive=Drive%20A&file=Folder%2FTemplate.docx";

    private static WordOnlineBusinessClient CreateTransportClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> onRequest)
    {
        var (credential, options) = TestHelpers.CreateMockedClientSetup(
            () => throw new InvalidOperationException("Unexpected fallback transport request."));
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns((HttpRequestMessage request, CancellationToken token) => onRequest(request, token));
        options.Transport = new HttpClientTransport(new HttpClient(handler.Object));
        return new WordOnlineBusinessClient(
            connectionRuntimeUrl: new Uri("https://test.azure.com/connection"),
            credential: credential,
            options: options);
    }

    private static WordOnlineBusinessFunctions CreateFunctions(WordOnlineBusinessClient client)
    {
        return new WordOnlineBusinessFunctions(TestHelpers.CreateNullLogger<WordOnlineBusinessFunctions>(), client);
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
    }

    [TestMethod]
    public async Task WordOnlineGetSourcesAsync_Discovery_ReturnsSourcesEnvelopeAndExactPath()
    {
        using var client = CreateTransportClient((request, _) =>
        {
            Assert.AreEqual(HttpMethod.Get, request.Method);
            Assert.AreEqual("/connection/codeless/v1.0/sources", request.RequestUri!.AbsolutePath);
            Assert.AreEqual(string.Empty, request.RequestUri.Query);
            return Task.FromResult(JsonResponse("{\"value\":[{\"name\":\"Synthetic Site\"}]}"));
        });

        var response = await CreateFunctions(client)
            .WordOnlineGetSourcesAsync(TestHelpers.CreateRequest(), CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(((MockHttpResponseData)response).GetBodyAsString());
        Assert.IsTrue(json.RootElement.GetProperty("success").GetBoolean());
        Assert.AreEqual("Synthetic Site", json.RootElement.GetProperty("sources").GetProperty("value")[0].GetProperty("name").GetString());
    }

    [TestMethod]
    public async Task WordOnlineGetDrivesAsync_Discovery_ReturnsDrivesEnvelopeAndExactPath()
    {
        using var client = CreateTransportClient((request, _) =>
        {
            Assert.AreEqual(HttpMethod.Get, request.Method);
            Assert.AreEqual("/connection/codeless/v1.0/drives?source=me", request.RequestUri!.PathAndQuery);
            return Task.FromResult(JsonResponse("{\"value\":[{\"name\":\"Synthetic Library\"}]}"));
        });

        var response = await CreateFunctions(client)
            .WordOnlineGetDrivesAsync(TestHelpers.CreateRequest(), CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(((MockHttpResponseData)response).GetBodyAsString());
        Assert.IsTrue(json.RootElement.GetProperty("success").GetBoolean());
        Assert.AreEqual("Synthetic Library", json.RootElement.GetProperty("drives").GetProperty("value")[0].GetProperty("name").GetString());
    }

    [TestMethod]
    public async Task WordOnlineGetFileSchemaAsync_WithSelectors_ReturnsDynamicSchemaAndEncodedPath()
    {
        using var client = CreateTransportClient((request, _) =>
        {
            Assert.AreEqual(HttpMethod.Get, request.Method);
            Assert.AreEqual("/connection/api/templates/schema?" + WordOnlineBusinessFunctionsTests.Selectors, request.RequestUri!.PathAndQuery);
            return Task.FromResult(JsonResponse("{\"fields\":{\"Customer\":{\"type\":\"string\"}}}"));
        });

        var response = await CreateFunctions(client)
            .WordOnlineGetFileSchemaAsync(TestHelpers.CreateRequest(url: "https://localhost/api/wordonline/templates/schema?" + WordOnlineBusinessFunctionsTests.Selectors), CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(((MockHttpResponseData)response).GetBodyAsString());
        Assert.IsTrue(json.RootElement.GetProperty("success").GetBoolean());
        Assert.AreEqual("string", json.RootElement.GetProperty("value").GetProperty("fields").GetProperty("Customer").GetProperty("type").GetString());
    }

    [TestMethod]
    public async Task WordOnlinePopulateTemplateAsync_WithDynamicFields_ReturnsRawWordBytes()
    {
        var expectedBytes = new byte[] { 0x50, 0x4b, 0x03, 0x04, 0x00, 0xff };
        using var client = CreateTransportClient(async (request, token) =>
        {
            Assert.AreEqual(HttpMethod.Post, request.Method);
            Assert.AreEqual("/connection/api/templates/getFile?" + WordOnlineBusinessFunctionsTests.Selectors, request.RequestUri!.PathAndQuery);
            Assert.AreEqual("application/json", request.Content!.Headers.ContentType!.MediaType);
            using var json = JsonDocument.Parse(await request.Content.ReadAsStringAsync(token)
                .ConfigureAwait(continueOnCapturedContext: false));
            Assert.AreEqual("Ada", json.RootElement.GetProperty("Customer").GetString());
            Assert.AreEqual("Synthetic SKU", json.RootElement.GetProperty("Items")[0].GetProperty("Sku").GetString());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(expectedBytes) };
        });

        var response = await CreateFunctions(client)
            .WordOnlinePopulateTemplateAsync(TestHelpers.CreateRequest(
                body: "{\"Customer\":\"Ada\",\"Items\":[{\"Sku\":\"Synthetic SKU\"}]}",
                method: "POST",
                url: "https://localhost/api/wordonline/templates/populate?" + WordOnlineBusinessFunctionsTests.Selectors), CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("application/vnd.openxmlformats-officedocument.wordprocessingml.document", response.Headers.Single(header => string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)).Value.Single());
        Assert.IsTrue(expectedBytes.SequenceEqual(((MemoryStream)response.Body).ToArray()));
    }

    [TestMethod]
    public async Task WordOnlineCreateWordFileWithContentAsync_WithHtmlAndFileName_ReturnsStringResult()
    {
        using var client = CreateTransportClient(async (request, token) =>
        {
            Assert.AreEqual(HttpMethod.Post, request.Method);
            Assert.AreEqual("/connection/api/templates/createWordFileWithContent?fileName=Synthetic%20Report.docx", request.RequestUri!.PathAndQuery);
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token)
                .ConfigureAwait(continueOnCapturedContext: false));
            Assert.AreEqual("<h1>Synthetic</h1>", json.RootElement.GetProperty("content").GetString());
            return JsonResponse("\"created-synthetic-file\"");
        });

        var response = await CreateFunctions(client)
            .WordOnlineCreateWordFileWithContentAsync(TestHelpers.CreateRequest(
                body: "{\"content\":\"<h1>Synthetic</h1>\"}", method: "POST",
                url: "https://localhost/api/wordonline/files/content?fileName=Synthetic%20Report.docx"), CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(((MockHttpResponseData)response).GetBodyAsString());
        Assert.IsTrue(json.RootElement.GetProperty("success").GetBoolean());
        Assert.AreEqual("created-synthetic-file", json.RootElement.GetProperty("value").GetString());
    }

    [TestMethod]
    public async Task WordOnlineGetFilePDFAsync_WithFlags_ReturnsRawPdfBytesAndExactQuery()
    {
        var expectedBytes = Encoding.ASCII.GetBytes("%PDF-1.7 synthetic\n%%EOF");
        using var client = CreateTransportClient((request, _) =>
        {
            Assert.AreEqual(HttpMethod.Get, request.Method);
            Assert.AreEqual("/connection/api/templates/convertFile?format=pdf&" + WordOnlineBusinessFunctionsTests.Selectors
                + "&extractSensitivityLabel=True&fetchSensitivityLabelMetadata=False", request.RequestUri!.PathAndQuery);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(expectedBytes) });
        });

        var response = await CreateFunctions(client)
            .WordOnlineGetFilePDFAsync(TestHelpers.CreateRequest(url: "https://localhost/api/wordonline/files/pdf?"
                + WordOnlineBusinessFunctionsTests.Selectors + "&extractSensitivityLabel=true&sensitivityLabelMetadata=false"), CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("application/pdf", response.Headers.Single(header => string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)).Value.Single());
        Assert.IsTrue(expectedBytes.SequenceEqual(((MemoryStream)response.Body).ToArray()));
    }

    [TestMethod]
    public async Task WordOnlineOperations_WithMissingSelectorsOrInvalidBodies_ReturnBadRequestWithoutConnectorCall()
    {
        var requestsSent = 0;
        using var client = CreateTransportClient((_, _) =>
        {
            requestsSent++;
            throw new InvalidOperationException("Validation should precede connector dispatch.");
        });
        var functions = CreateFunctions(client);
        var missing = TestHelpers.CreateRequest(url: "https://localhost/api/wordonline/templates/schema?source=me&drive=Drive%20A");
        var noSelectors = TestHelpers.CreateRequest(body: "{}", method: "POST");
        var malformed = TestHelpers.CreateRequest(body: "{", method: "POST", url: "https://localhost/api/test?" + WordOnlineBusinessFunctionsTests.Selectors);
        var invalidShape = TestHelpers.CreateRequest(body: "[]", method: "POST", url: "https://localhost/api/test?" + WordOnlineBusinessFunctionsTests.Selectors);
        var noContent = TestHelpers.CreateRequest(body: "{\"content\":\" \"}", method: "POST");
        var invalidPdf = TestHelpers.CreateRequest(url: "https://localhost/api/test?" + WordOnlineBusinessFunctionsTests.Selectors + "&extractSensitivityLabel=perhaps");
        var missingPdf = TestHelpers.CreateRequest(url: "https://localhost/api/test?source=me&drive=Drive%20A");

        var responses = new[]
        {
            await functions
                .WordOnlineGetFileSchemaAsync(request: missing, cancellationToken: CancellationToken.None)
                .ConfigureAwait(continueOnCapturedContext: false),
            await functions
                .WordOnlinePopulateTemplateAsync(request: noSelectors, cancellationToken: CancellationToken.None)
                .ConfigureAwait(continueOnCapturedContext: false),
            await functions
                .WordOnlinePopulateTemplateAsync(request: malformed, cancellationToken: CancellationToken.None)
                .ConfigureAwait(continueOnCapturedContext: false),
            await functions
                .WordOnlinePopulateTemplateAsync(request: invalidShape, cancellationToken: CancellationToken.None)
                .ConfigureAwait(continueOnCapturedContext: false),
            await functions
                .WordOnlineCreateWordFileWithContentAsync(request: noContent, cancellationToken: CancellationToken.None)
                .ConfigureAwait(continueOnCapturedContext: false),
            await functions
                .WordOnlineGetFilePDFAsync(request: invalidPdf, cancellationToken: CancellationToken.None)
                .ConfigureAwait(continueOnCapturedContext: false),
            await functions
                .WordOnlineGetFilePDFAsync(request: missingPdf, cancellationToken: CancellationToken.None)
                .ConfigureAwait(continueOnCapturedContext: false),
        };

        foreach (var response in responses)
        {
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.IsTrue(((MockHttpResponseData)response).GetBodyAsString().Contains("\"success\":false", StringComparison.Ordinal));
        }

        Assert.AreEqual(0, requestsSent);
    }

    [TestMethod]
    [Owner("daviburg")]
    [DataRow("populate", false)]
    [DataRow("pdf", false)]
    [DataRow("populate", true)]
    [DataRow("pdf", true)]
    public async Task WordOnlineBinaryOperations_WhenTransportOrCredentialFails_ReturnStructuredInternalServerError(
        string operation,
        bool credentialFailure)
    {
        var requestsSent = 0;
        var (credential, options) = TestHelpers.CreateMockedClientSetup(() =>
        {
            requestsSent++;
            throw new HttpRequestException(message: "Synthetic transport failure.");
        });
        options.Retry.MaxRetries = 0;
        if (credentialFailure)
        {
            Mock.Get(credential)
                .Setup(value => value.GetTokenAsync(
                    It.IsAny<TokenRequestContext>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new AuthenticationFailedException(message: "Synthetic credential failure."));
        }

        using var client = new WordOnlineBusinessClient(
            connectionRuntimeUrl: new Uri("https://test.azure.com/connection"),
            credential: credential,
            options: options);
        var functions = WordOnlineBusinessFunctionsTests.CreateFunctions(client);
        var request = TestHelpers.CreateRequest(
            body: "{\"Customer\":\"Ada\"}",
            method: string.Equals(operation, "populate", StringComparison.Ordinal) ? "POST" : "GET",
            url: "https://localhost/api/test?" + WordOnlineBusinessFunctionsTests.Selectors);

        var response = string.Equals(operation, "populate", StringComparison.Ordinal)
            ? await functions
                .WordOnlinePopulateTemplateAsync(request, CancellationToken.None)
                .ConfigureAwait(continueOnCapturedContext: false)
            : await functions
                .WordOnlineGetFilePDFAsync(request, CancellationToken.None)
                .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: HttpStatusCode.InternalServerError, actual: response.StatusCode);
        using var json = JsonDocument.Parse(((MockHttpResponseData)response).GetBodyAsString());
        Assert.IsFalse(json.RootElement.GetProperty("success").GetBoolean());
        StringAssert.Contains(
            value: json.RootElement.GetProperty("error").GetString()!,
            substring: credentialFailure ? "Synthetic credential failure." : "Synthetic transport failure.");
        Assert.AreEqual(expected: credentialFailure ? 0 : 1, actual: requestsSent);
    }

    [TestMethod]
    [DataRow("schema")]
    [DataRow("populate")]
    [DataRow("content")]
    [DataRow("pdf")]
    public async Task WordOnlineOperations_WhenConnectorDeniesAccess_ReturnStructuredBadGateway(string operation)
    {
        using var client = CreateTransportClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{\"error\":\"synthetic permission denied\"}"),
        }));
        var functions = CreateFunctions(client);
        var request = TestHelpers.CreateRequest(
            body: operation == "populate" ? "{\"Customer\":\"Ada\"}" : "{\"content\":\"<p>Synthetic</p>\"}",
            method: operation is "populate" or "content" ? "POST" : "GET",
            url: "https://localhost/api/test?" + WordOnlineBusinessFunctionsTests.Selectors);

        var response = operation switch
        {
            "schema" => await functions
                .WordOnlineGetFileSchemaAsync(request: request, cancellationToken: CancellationToken.None)
                .ConfigureAwait(continueOnCapturedContext: false),
            "populate" => await functions
                .WordOnlinePopulateTemplateAsync(request: request, cancellationToken: CancellationToken.None)
                .ConfigureAwait(continueOnCapturedContext: false),
            "content" => await functions
                .WordOnlineCreateWordFileWithContentAsync(request: request, cancellationToken: CancellationToken.None)
                .ConfigureAwait(continueOnCapturedContext: false),
            "pdf" => await functions
                .WordOnlineGetFilePDFAsync(request: request, cancellationToken: CancellationToken.None)
                .ConfigureAwait(continueOnCapturedContext: false),
            _ => throw new InvalidOperationException("Unexpected test operation."),
        };

        Assert.AreEqual(HttpStatusCode.BadGateway, response.StatusCode);
        using var json = JsonDocument.Parse(((MockHttpResponseData)response).GetBodyAsString());
        Assert.IsFalse(json.RootElement.GetProperty("success").GetBoolean());
        Assert.AreEqual(403, json.RootElement.GetProperty("statusCode").GetInt32());
        Assert.IsTrue(json.RootElement.GetProperty("details").GetString()!.Contains("synthetic permission denied", StringComparison.Ordinal));
    }
}
