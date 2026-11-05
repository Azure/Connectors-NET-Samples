//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Azure.Connectors.Sdk.OneDriveForBusiness;
using Azure.Core.Pipeline;

namespace DirectConnector.Tests;

[TestClass]
public class OneDriveOperationFunctionsTests
{
    [TestMethod]
    public async Task DeleteOneDriveFileAsync_WithIdentifier_DispatchesGeneratedDelete()
    {
        using var handler = new DeleteHandler();
        using var client = OneDriveOperationFunctionsTests.CreateClient(handler);
        var functions = new OneDriveFunctions(TestHelpers.CreateNullLogger<OneDriveFunctions>(), client);
        var response = await functions
            .DeleteOneDriveFileAsync(TestHelpers.CreateRequest(url: "https://localhost/api/onedrive/files?file=owned%2Ffile"), CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
        Assert.AreEqual(expected: HttpStatusCode.OK, actual: response.StatusCode);
        Assert.AreEqual(expected: "DELETE", actual: handler.Method);
        Assert.AreEqual(expected: "/connection/datasets/default/files/owned%252Ffile", actual: handler.Path);
        Assert.AreEqual(expected: 1, actual: handler.Requests);
        using var body = JsonDocument.Parse(((MockHttpResponseData)response).GetBodyAsString());
        Assert.IsTrue(body.RootElement.GetProperty("value").GetProperty("deleted").GetBoolean());
    }

    [TestMethod]
    public async Task DeleteOneDriveFileAsync_WithoutIdentifier_RejectsBeforeDispatch()
    {
        using var handler = new DeleteHandler();
        using var client = OneDriveOperationFunctionsTests.CreateClient(handler);
        var functions = new OneDriveFunctions(TestHelpers.CreateNullLogger<OneDriveFunctions>(), client);
        var response = await functions
            .DeleteOneDriveFileAsync(TestHelpers.CreateRequest(), CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
        Assert.AreEqual(expected: HttpStatusCode.BadRequest, actual: response.StatusCode);
        Assert.AreEqual(expected: 0, actual: handler.Requests);
    }

    [TestMethod]
    public async Task DeleteOneDriveFileAsync_WithForbidden_ReturnsStructuredPermissionError()
    {
        using var handler = new DeleteHandler { Status = HttpStatusCode.Forbidden };
        using var client = OneDriveOperationFunctionsTests.CreateClient(handler);
        var functions = new OneDriveFunctions(TestHelpers.CreateNullLogger<OneDriveFunctions>(), client);
        var response = await functions
            .DeleteOneDriveFileAsync(TestHelpers.CreateRequest(url: "https://localhost/api/onedrive/files?file=owned-file"), CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
        Assert.AreEqual(expected: HttpStatusCode.BadGateway, actual: response.StatusCode);
        using var body = JsonDocument.Parse(((MockHttpResponseData)response).GetBodyAsString());
        Assert.AreEqual(expected: 403, actual: body.RootElement.GetProperty("statusCode").GetInt32());
        Assert.IsFalse(body.RootElement.GetProperty("success").GetBoolean());
    }

    [TestMethod]
    public async Task GetOneDriveFileMetadataAsync_WithIdentifier_ReturnsLocatorFromGeneratedClient()
    {
        using var handler = new DeleteHandler { Status = HttpStatusCode.OK };
        using var client = OneDriveOperationFunctionsTests.CreateClient(handler);
        var functions = new OneDriveFunctions(TestHelpers.CreateNullLogger<OneDriveFunctions>(), client);
        var response = await functions
            .GetOneDriveFileMetadataAsync(TestHelpers.CreateRequest(url: "https://localhost/api/onedrive/metadata?file=owned-file"), CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
        Assert.AreEqual(expected: HttpStatusCode.OK, actual: response.StatusCode);
        Assert.AreEqual(expected: "GET", actual: handler.Method);
        Assert.AreEqual(expected: "/connection/datasets/default/files/owned-file", actual: handler.Path);
        using var body = JsonDocument.Parse(((MockHttpResponseData)response).GetBodyAsString());
        Assert.AreEqual(expected: "synthetic-locator", actual: body.RootElement.GetProperty("value").GetProperty("FileLocator").GetString());
    }

    [TestMethod]
    public async Task GetOneDriveFileMetadataAsync_WithoutIdentifier_RejectsBeforeDispatch()
    {
        using var handler = new DeleteHandler();
        using var client = OneDriveOperationFunctionsTests.CreateClient(handler);
        var functions = new OneDriveFunctions(TestHelpers.CreateNullLogger<OneDriveFunctions>(), client);
        var response = await functions
            .GetOneDriveFileMetadataAsync(TestHelpers.CreateRequest(), CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
        Assert.AreEqual(expected: HttpStatusCode.BadRequest, actual: response.StatusCode);
        Assert.AreEqual(expected: 0, actual: handler.Requests);
    }

    private static OneDriveForBusinessClient CreateClient(DeleteHandler handler)
    {
        var setup = TestHelpers.CreateMockedClientSetup(responseFactory: () => new HttpResponseMessage(statusCode: HttpStatusCode.OK));
        setup.Options.Transport = new HttpClientTransport(new HttpClient(handler));
        return new OneDriveForBusinessClient(connectionRuntimeUrl: new Uri(uriString: "https://test.azure.com/connection"), credential: setup.Credential, options: setup.Options);
    }

    private sealed class DeleteHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; init; } = HttpStatusCode.NoContent;
        public string? Method { get; private set; }
        public string? Path { get; private set; }
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.Requests++;
            this.Method = request.Method.Method;
            this.Path = request.RequestUri!.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(statusCode: this.Status)
            {
                Content = new StringContent(content: "{\"Id\":\"owned-file\",\"FileLocator\":\"synthetic-locator\"}"),
            });
        }
    }
}
