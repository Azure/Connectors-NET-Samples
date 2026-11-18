//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Azure.Connectors.Sdk.SharePointOnline;

namespace DirectConnector.Tests;

[TestClass]
public class SharePointFunctionsTests
{
    private static SharePointOnlineClient CreateMockedClient(Func<HttpResponseMessage> responseFactory)
    {
        var (credential, options) = TestHelpers.CreateMockedClientSetup(responseFactory);
        return new SharePointOnlineClient(
            connectionRuntimeUrl: new Uri("https://test.azure.com/connection"),
            credential: credential,
            options: options);
    }

    [TestMethod]
    public async Task ListSharePointSitesAsync_WithValidResponse_ReturnsSites()
    {
        var sitesResponse = new
        {
            value = new[]
            {
                new { Name = "https://contoso.sharepoint.com/sites/testsite", DisplayName = "Test site" },
            },
        };
        using var client = CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(sitesResponse)),
        });
        var functions = new SharePointFunctions(
            TestHelpers.CreateNullLogger<SharePointFunctions>(),
            client);
        var request = TestHelpers.CreateRequest(url: "https://localhost/api/sharepoint/sites");

        var response = await functions.ListSharePointSitesAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: HttpStatusCode.OK, actual: response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(condition: body.Contains("Test site", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GetSharePointListsAsync_WithValidResponse_ReturnsLists()
    {
        // Arrange — sanitized payload based on real SharePoint lists response
        var listsResponse = new
        {
            value = new[]
            {
                new { Name = "Documents", DisplayName = "Documents", IsFolder = false },
                new { Name = "Site Assets", DisplayName = "Site Assets", IsFolder = false },
            },
        };

        using var client = CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(listsResponse)),
        });

        var functions = new SharePointFunctions(
            TestHelpers.CreateNullLogger<SharePointFunctions>(),
            client);

        var request = TestHelpers.CreateRequest(
            url: "https://localhost/api/sharepoint/lists?site=https://contoso.sharepoint.com/sites/testsite");

        // Act
        var response = await functions.GetSharePointListsAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        // Assert
        Assert.AreEqual(expected: HttpStatusCode.OK, actual: response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(condition: body.Contains("Documents", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GetSharePointListsAsync_WithConnectorError_Returns502()
    {
        // Arrange
        using var client = CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.NotFound,
            Content = new StringContent("{\"error\":{\"code\":\"itemNotFound\",\"message\":\"Site not found\"}}"),
        });

        var functions = new SharePointFunctions(
            TestHelpers.CreateNullLogger<SharePointFunctions>(),
            client);

        var request = TestHelpers.CreateRequest(
            url: "https://localhost/api/sharepoint/lists?site=https://contoso.sharepoint.com/sites/testsite");

        // Act
        var response = await functions.GetSharePointListsAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        // Assert
        Assert.AreEqual(expected: HttpStatusCode.BadGateway, actual: response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(condition: body.Contains("\"success\":false", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ListFolderAsync_WithValidPath_ReturnsItems()
    {
        // Arrange — sanitized payload based on real SharePoint folder listing
        var folderResponse = new
        {
            value = new[]
            {
                new { Name = "Report.docx", DisplayName = "Report.docx", IsFolder = false, Path = "/Shared Documents/Report.docx" },
                new { Name = "Archive", DisplayName = "Archive", IsFolder = true, Path = "/Shared Documents/Archive" },
            },
        };

        using var client = CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(folderResponse)),
        });

        var functions = new SharePointFunctions(
            TestHelpers.CreateNullLogger<SharePointFunctions>(),
            client);

        var request = TestHelpers.CreateRequest(
            url: "https://localhost/api/sharepoint/files?site=https://contoso.sharepoint.com/sites/testsite&library=Documents&folder=/Shared%20Documents");

        // Act
        var response = await functions.ListFolderAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        // Assert
        Assert.AreEqual(expected: HttpStatusCode.OK, actual: response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(condition: body.Contains("Report.docx", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ListFolderAsync_WithTwoPages_ReturnsItemsFromBothPages()
    {
        // Arrange — two pages linked by @odata.nextLink
        var responseBodies = new Queue<string>(new[]
        {
            "{\"value\":[{\"{Name}\":\"first-page.docx\"}],\"@odata.nextLink\":\"https://test.azure.com/connection/next\"}",
            "{\"value\":[{\"{Name}\":\"second-page.docx\"}]}",
        });
        using var client = CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(responseBodies.Dequeue()),
        });
        var functions = new SharePointFunctions(TestHelpers.CreateNullLogger<SharePointFunctions>(), client);
        var request = TestHelpers.CreateRequest(url: "https://localhost/api/sharepoint/files?site=https://contoso.sharepoint.com/sites/testsite");

        // Act
        var response = await functions.ListFolderAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        // Assert
        Assert.AreEqual(expected: HttpStatusCode.OK, actual: response.StatusCode);
        using var result = JsonDocument.Parse(((MockHttpResponseData)response).GetBodyAsString());
        Assert.AreEqual(expected: 2, actual: result.RootElement.GetProperty("count").GetInt32());
        var body = result.RootElement.GetRawText();
        Assert.IsTrue(condition: body.Contains("first-page.docx", StringComparison.Ordinal));
        Assert.IsTrue(condition: body.Contains("second-page.docx", StringComparison.Ordinal));
        Assert.AreEqual(expected: 0, actual: responseBodies.Count);
    }

    [TestMethod]
    public async Task ListFolderAsync_WithMissingSite_Returns400()
    {
        // Arrange — no site query parameter (site is required; library defaults to "Documents")
        using var client = CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("{}"),
        });

        var functions = new SharePointFunctions(
            TestHelpers.CreateNullLogger<SharePointFunctions>(),
            client);

        var request = TestHelpers.CreateRequest(url: "https://localhost/api/sharepoint/files");

        // Act
        var response = await functions.ListFolderAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        // Assert
        Assert.AreEqual(expected: HttpStatusCode.BadRequest, actual: response.StatusCode);
    }

    [TestMethod]
    public async Task SharePointNewFileTriggerAsync_WithEmptyBody_Returns400()
    {
        using var client = CreateMockedClient(() => new HttpResponseMessage(HttpStatusCode.OK));
        var functions = new SharePointFunctions(
            TestHelpers.CreateNullLogger<SharePointFunctions>(),
            client);

        var request = TestHelpers.CreateRequest(body: "");

        var response = await functions.SharePointNewFileTriggerAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: HttpStatusCode.BadRequest, actual: response.StatusCode);
    }

    [TestMethod]
    public async Task SharePointNewFileTriggerAsync_WithInvalidJson_Returns400()
    {
        using var client = CreateMockedClient(() => new HttpResponseMessage(HttpStatusCode.OK));
        var functions = new SharePointFunctions(
            TestHelpers.CreateNullLogger<SharePointFunctions>(),
            client);

        var request = TestHelpers.CreateRequest(body: "not valid json {{{");

        var response = await functions.SharePointNewFileTriggerAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: HttpStatusCode.BadRequest, actual: response.StatusCode);
    }

    [TestMethod]
    public async Task SharePointNewFileTriggerAsync_WithValidPayload_Returns200()
    {
        using var client = CreateMockedClient(() => new HttpResponseMessage(HttpStatusCode.OK));
        var functions = new SharePointFunctions(
            TestHelpers.CreateNullLogger<SharePointFunctions>(),
            client);

        var payload = """{"body":{"value":[{"dynamicProperties":{}}]}}""";
        var request = TestHelpers.CreateRequest(body: payload);

        var response = await functions.SharePointNewFileTriggerAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(expected: HttpStatusCode.OK, actual: response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(condition: body.Contains("\"itemCount\":1", StringComparison.Ordinal));
    }
}
