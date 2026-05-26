//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Azure.Connectors.Sdk.Office365;

namespace DirectConnector.Tests;

[TestClass]
public class Office365FunctionsTests
{
    private static Office365Client CreateMockedClient(Func<HttpResponseMessage> responseFactory)
    {
        var (credential, options) = TestHelpers.CreateMockedClientSetup(responseFactory);
        return new Office365Client(
            connectionRuntimeUrl: new Uri("https://test.azure.com/connection"),
            credential: credential,
            options: options);
    }

    [TestMethod]
    public async Task GetCategoriesAsync_WithValidResponse_ReturnsCategories()
    {
        // Arrange — sanitized payload based on real Outlook category response
        var categories = new[]
        {
            new { Id = "cat-001", DisplayName = "Red Category", Color = "preset0" },
            new { Id = "cat-002", DisplayName = "Blue Category", Color = "preset7" },
            new { Id = "cat-003", DisplayName = "Green Category", Color = "preset4" },
        };

        using var client = CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(categories)),
        });

        var functions = new Office365Functions(
            TestHelpers.CreateNullLogger<Office365Functions>(),
            client);

        var request = TestHelpers.CreateRequest();

        // Act
        var response = await functions.GetCategoriesAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("Red Category", StringComparison.Ordinal));
        Assert.IsTrue(body.Contains("Blue Category", StringComparison.Ordinal));
        Assert.IsTrue(body.Contains("Green Category", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GetCategoriesAsync_WithConnectorError_Returns502()
    {
        // Arrange — simulate a connector error
        using var client = CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.Unauthorized,
            Content = new StringContent("{\"error\":{\"code\":\"AuthenticationFailed\",\"message\":\"Token expired\"}}"),
        });

        var functions = new Office365Functions(
            TestHelpers.CreateNullLogger<Office365Functions>(),
            client);

        var request = TestHelpers.CreateRequest();

        // Act
        var response = await functions.GetCategoriesAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadGateway, response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("\"success\":false", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SendEmailAsync_WithValidPayload_Returns200()
    {
        // Arrange — sanitized email request
        var emailPayload = JsonSerializer.Serialize(new
        {
            To = "testuser@contoso.com",
            Subject = "Unit Test Email",
            Body = "<p>This is a test.</p>",
        });

        using var client = CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("{}"),
        });

        var functions = new Office365Functions(
            TestHelpers.CreateNullLogger<Office365Functions>(),
            client);

        var request = TestHelpers.CreateRequest(body: emailPayload, method: "POST");

        // Act
        var response = await functions.SendEmailAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("\"success\":true", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SendEmailAsync_WithInvalidJson_Returns400()
    {
        // Arrange — malformed JSON
        using var client = CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("{}"),
        });

        var functions = new Office365Functions(
            TestHelpers.CreateNullLogger<Office365Functions>(),
            client);

        var request = TestHelpers.CreateRequest(body: "{ invalid json !!!", method: "POST");

        // Act
        var response = await functions.SendEmailAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task SendEmailAsync_WithMissingFields_Returns400()
    {
        // Arrange — valid JSON but missing required fields
        using var client = CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("{}"),
        });

        var functions = new Office365Functions(
            TestHelpers.CreateNullLogger<Office365Functions>(),
            client);

        var request = TestHelpers.CreateRequest(body: "{}", method: "POST");

        // Act
        var response = await functions.SendEmailAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("error", StringComparison.OrdinalIgnoreCase));
    }
}
