//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text.Json;
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
    public async Task GetTeamChannelsAsync_WithMissingTeamId_Returns400()
    {
        // Arrange — no teamId query parameter
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
    public async Task GetTeamChannelsAsync_WithTeamId_ReturnsChannels()
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

    /// <summary>
    /// Demonstrates SDK v0.12.0 init-only setters and ModelFactory pattern.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Output-only model properties (e.g., <c>ETag</c>, <c>CreatedDateTime</c>) changed from
    /// <c>{ get; internal set; }</c> to <c>{ get; init; }</c> in v0.12.0. External code can no
    /// longer do <c>model.ETag = "...";</c> — use object initializers or the per-connector
    /// <c>*ModelFactory</c> class to construct models with output-only properties in tests.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void ModelFactory_GetChannelResponse_SetsInitOnlyProperties()
    {
        // Arrange + Act — use TeamsModelFactory to set init-only output properties.
        // This is the recommended pattern for constructing test fixtures since v0.12.0.
        var channel = TeamsModelFactory.GetChannelResponse(
            channelId: "19:general@thread.tacv2",
            displayName: "General",
            descriptionOfChannel: "The default channel",
            theEmailAddressForTheChannel: "general@contoso.com");

        // Assert — init-only properties are set via the factory method.
        Assert.AreEqual("19:general@thread.tacv2", channel.ChannelId);
        Assert.AreEqual("General", channel.DisplayName);
        Assert.AreEqual("The default channel", channel.DescriptionOfChannel);
        Assert.AreEqual("general@contoso.com", channel.TheEmailAddressForTheChannel);
    }

    /// <summary>
    /// Demonstrates SDK v0.12.0 JsonElement? property handling.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Dynamic model properties changed from <c>object</c> to <c>JsonElement?</c> in v0.12.0.
    /// Properties like <c>GetAllTeamsResponse.TeamsList</c> are now <c>List&lt;JsonElement?&gt;</c>.
    /// Use <c>TryGetProperty</c> and <c>GetString</c>/<c>GetInt32</c> to extract typed values.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void JsonElementProperties_ExtractTypedValues_FromDynamicResponse()
    {
        // Arrange — simulate a Teams API response with JsonElement? list items
        var json = """
            {
                "@odata.context": "https://graph.microsoft.com/v1.0/$metadata",
                "value": [
                    { "id": "team-001", "displayName": "Engineering", "description": "Dev team" },
                    { "id": "team-002", "displayName": "Marketing", "description": null }
                ]
            }
            """;

        var response = JsonSerializer.Deserialize<GetAllTeamsResponse>(json);

        // Act — extract typed values from List<JsonElement?> using TryGetProperty/GetString
        var teams = response?.TeamsList?
            .Where(element => element.HasValue)
            .Select(element =>
            {
                var team = element!.Value;
                return new
                {
                    Id = team.TryGetProperty("id", out var id) ? id.GetString() : null,
                    Name = team.TryGetProperty("displayName", out var name) ? name.GetString() : null,
                };
            })
            .ToList();

        // Assert
        Assert.IsNotNull(teams);
        Assert.AreEqual(2, teams.Count);
        Assert.AreEqual("team-001", teams[0].Id);
        Assert.AreEqual("Engineering", teams[0].Name);
        Assert.AreEqual("team-002", teams[1].Id);
        Assert.AreEqual("Marketing", teams[1].Name);
    }

    [TestMethod]
    public async Task TeamsChannelMessageTriggerAsync_WithEmptyBody_Returns400()
    {
        using var client = CreateMockedClient(() => new HttpResponseMessage(HttpStatusCode.OK));
        var functions = new TeamsFunctions(
            TestHelpers.CreateNullLogger<TeamsFunctions>(),
            client);

        var request = TestHelpers.CreateRequest(body: "");

        var response = await functions.TeamsChannelMessageTriggerAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task TeamsChannelMessageTriggerAsync_WithInvalidJson_Returns400()
    {
        using var client = CreateMockedClient(() => new HttpResponseMessage(HttpStatusCode.OK));
        var functions = new TeamsFunctions(
            TestHelpers.CreateNullLogger<TeamsFunctions>(),
            client);

        var request = TestHelpers.CreateRequest(body: "not valid json {{{");

        var response = await functions.TeamsChannelMessageTriggerAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task TeamsChannelMessageTriggerAsync_WithValidPayload_Returns200()
    {
        using var client = CreateMockedClient(() => new HttpResponseMessage(HttpStatusCode.OK));
        var functions = new TeamsFunctions(
            TestHelpers.CreateNullLogger<TeamsFunctions>(),
            client);

        var payload = """{"body":{"value":[{"id":"msg-1","body":{"content":"Hello"}}]}}""";
        var request = TestHelpers.CreateRequest(body: payload);

        var response = await functions.TeamsChannelMessageTriggerAsync(request, CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = ((MockHttpResponseData)response).GetBodyAsString();
        Assert.IsTrue(body.Contains("\"messageCount\":1", StringComparison.Ordinal));
    }
}
