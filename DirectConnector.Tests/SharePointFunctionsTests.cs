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
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("Documents", StringComparison.Ordinal));
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
        Assert.AreEqual(HttpStatusCode.BadGateway, response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("\"success\":false", StringComparison.Ordinal));
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
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("Report.docx", StringComparison.Ordinal));
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
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
