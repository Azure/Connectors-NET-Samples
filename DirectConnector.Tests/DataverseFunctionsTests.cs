//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Azure.Connectors.Sdk.Commondataservice;

namespace DirectConnector.Tests;

[TestClass]
public class DataverseFunctionsTests
{
    private static CommondataserviceClient CreateMockedClient(Func<HttpResponseMessage> responseFactory)
    {
        var (credential, options) = TestHelpers.CreateMockedClientSetup(responseFactory);
        return new CommondataserviceClient(
            connectionRuntimeUrl: new Uri("https://test.azure.com/connection"),
            credential: credential,
            options: options);
    }

    [TestMethod]
    public async Task GetNextPageAsync_WithNextLink_ReturnsConnectorResponse()
    {
        using var client = CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("{\"value\":[{\"name\":\"next-page-item\"}]}"),
        });
        var functions = new DataverseFunctions(
            TestHelpers.CreateNullLogger<DataverseFunctions>(),
            client);
        var nextLink = Uri.EscapeDataString("https://example.test/next?page=2");
        var request = TestHelpers.CreateRequest(
            url: $"https://localhost/api/dataverse/nextpage?nextLink={nextLink}");

        var response = await functions.GetNextPageAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("next-page-item", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CreateAttachmentAsync_WithBinaryBody_ReturnsAttachment()
    {
        var attachmentResponse = new
        {
            annotationid = "attachment-id",
            subject = "sample-note.txt",
        };
        using var client = CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(attachmentResponse)),
        });
        var functions = new DataverseFunctions(
            TestHelpers.CreateNullLogger<DataverseFunctions>(),
            client);
        var environment = Uri.EscapeDataString("https://example.crm.dynamics.com");
        var request = TestHelpers.CreateRequest(
            body: "attachment content",
            method: "POST",
            url: $"https://localhost/api/dataverse/items/record-id/attachments?environment={environment}&tableName=accounts&fileName=sample-note.txt");

        var response = await functions.CreateAttachmentAsync(
            request,
            itemIdentifier: "record-id",
            cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("attachment-id", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CreateAttachmentAsync_WithMissingFileName_Returns400()
    {
        using var client = CreateMockedClient(() => new HttpResponseMessage(HttpStatusCode.OK));
        var functions = new DataverseFunctions(
            TestHelpers.CreateNullLogger<DataverseFunctions>(),
            client);
        var environment = Uri.EscapeDataString("https://example.crm.dynamics.com");
        var request = TestHelpers.CreateRequest(
            body: "attachment content",
            method: "POST",
            url: $"https://localhost/api/dataverse/items/record-id/attachments?environment={environment}&tableName=accounts");

        var response = await functions.CreateAttachmentAsync(
            request,
            itemIdentifier: "record-id",
            cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
