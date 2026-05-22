//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Azure.Connectors.Sdk;
using Azure.Connectors.Sdk.Teams;
using Azure.Connectors.Sdk.Teams.Models;

namespace DirectConnector.Tests;

[TestClass]
public class TeamsFunctionsTests
{
    private static TeamsClient CreateMockedClient(Func<HttpResponseMessage> responseFactory)
    {
        var (credential, options) = TestHelpers.CreateMockedClientSetup(responseFactory);
        return new TeamsClient(
            connectionRuntimeUrl: new Uri("https://test.azure.com/connection"),
            credential: credential,
            options: options);
    }

    [TestMethod]
    public async Task GetAllTeamsAsync_WithValidResponse_ReturnsTeams()
    {
        // Arrange — sanitized payload based on real Teams response
        var teamsResponse = new
        {
            value = new[]
            {
                new { id = "team-001", displayName = "Engineering", description = "Engineering team" },
                new { id = "team-002", displayName = "Design", description = "Design team" },
            },
        };

        using var client = CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(teamsResponse)),
        });

        var functions = new TeamsFunctions(
            TestHelpers.CreateNullLogger<TeamsFunctions>(),
            client);

        var request = TestHelpers.CreateRequest();

        // Act
        var response = await functions.GetAllTeamsAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("Engineering", StringComparison.Ordinal));
        Assert.IsTrue(body.Contains("Design", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GetAllTeamsAsync_WithConnectorError_Returns502()
    {
        // Arrange
        using var client = CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.Forbidden,
            Content = new StringContent("{\"error\":{\"code\":\"Forbidden\",\"message\":\"Insufficient permissions\"}}"),
        });

        var functions = new TeamsFunctions(
            TestHelpers.CreateNullLogger<TeamsFunctions>(),
            client);

        var request = TestHelpers.CreateRequest();

        // Act
        var response = await functions.GetAllTeamsAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadGateway, response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("\"success\":false", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GetTeamChannelsAsync_WithMissingGroupId_Returns400()
    {
        // Arrange — no groupId query parameter
        using var client = CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("{}"),
        });

        var functions = new TeamsFunctions(
            TestHelpers.CreateNullLogger<TeamsFunctions>(),
            client);

        var request = TestHelpers.CreateRequest(url: "https://localhost/api/teams/channels");

        // Act
        var response = await functions.GetTeamChannelsAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        // Assert
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetTeamChannelsAsync_WithGroupId_ReturnsChannels()
    {
        // Arrange — sanitized payload based on real Teams channels response
        var channelsResponse = new
        {
            value = new[]
            {
                new
                {
                    id = "19:channel001@thread.tacv2",
                    displayName = "General",
                    description = "General discussion",
                    membershipType = "standard",
                },
                new
                {
                    id = "19:channel002@thread.tacv2",
                    displayName = "Engineering",
                    description = "Engineering channel",
                    membershipType = "standard",
                },
            },
        };

        using var client = CreateMockedClient(() => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(channelsResponse)),
        });

        var functions = new TeamsFunctions(
            TestHelpers.CreateNullLogger<TeamsFunctions>(),
            client);

        var request = TestHelpers.CreateRequest(
            url: "https://localhost/api/teams/channels?teamId=00000000-0000-0000-0000-000000000001");

        // Act
        var response = await functions.GetTeamChannelsAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("General", StringComparison.Ordinal));
        Assert.IsTrue(body.Contains("Engineering", StringComparison.Ordinal));
    }
}
