//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using Azure.Connectors.Sdk.Planner;

namespace DirectConnector.Tests;

[TestClass]
public class PlannerFunctionsTests
{
    private static PlannerClient CreateMockedClient(Func<HttpResponseMessage> responseFactory)
    {
        var (credential, options) = TestHelpers.CreateMockedClientSetup(responseFactory);
        return new PlannerClient(
            connectionRuntimeUrl: new Uri("https://test.azure.com/connection"),
            credential: credential,
            options: options);
    }

    [TestMethod]
    public async Task PlannerListGroupsAsync_WithValidResponse_ReturnsGroups()
    {
        using var client = PlannerFunctionsTests.CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("{\"value\":[{\"id\":\"group-1\",\"displayName\":\"SDK validation\"}]}"),
        });
        var functions = new PlannerFunctions(TestHelpers.CreateNullLogger<PlannerFunctions>(), client);

        var response = await functions
            .PlannerListGroupsAsync(TestHelpers.CreateRequest(), CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsTrue(((MockHttpResponseData)response).GetBodyAsString().Contains("SDK validation", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task PlannerListMyTasksAsync_WithValidResponse_ReturnsVersionedTasks()
    {
        using var client = PlannerFunctionsTests.CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("{\"value\":[{\"id\":\"task-1\",\"title\":\"Validate SDK 0.14\",\"percentComplete\":0}]}"),
        });
        var functions = new PlannerFunctions(TestHelpers.CreateNullLogger<PlannerFunctions>(), client);

        var response = await functions
            .PlannerListMyTasksAsync(TestHelpers.CreateRequest(), CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsTrue(((MockHttpResponseData)response).GetBodyAsString().Contains("Validate SDK 0.14", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task PlannerListGroupsAsync_WithConnectorError_ReturnsBadGateway()
    {
        using var client = PlannerFunctionsTests.CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.Forbidden,
            Content = new StringContent("{\"error\":{\"code\":\"Forbidden\",\"message\":\"Access denied\"}}"),
        });
        var functions = new PlannerFunctions(TestHelpers.CreateNullLogger<PlannerFunctions>(), client);

        var response = await functions
            .PlannerListGroupsAsync(TestHelpers.CreateRequest(), CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(HttpStatusCode.BadGateway, response.StatusCode);
    }
}