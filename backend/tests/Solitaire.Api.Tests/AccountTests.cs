using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Solitaire.Api.Tests;

public class AccountTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory = factory;

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private async Task<HttpClient> RegisteredClientAsync(string name)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { username = name, email = $"{name}@example.com", password = "Password123!" });
        response.EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<string> CsrfTokenAsync(HttpClient client)
    {
        var body = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        return body.GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task Export_ReturnsTheAccountsData_AsJsonDownload()
    {
        var client = await RegisteredClientAsync("exporter");

        var response = await client.GetAsync("/api/account/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);

        var package = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), WebJson);
        Assert.Equal("exporter", package.GetProperty("account").GetProperty("userName").GetString());
        Assert.True(package.TryGetProperty("stats", out _));
        Assert.True(package.TryGetProperty("saves", out _));
        Assert.True(package.TryGetProperty("leaderboard", out _));
    }

    [Fact]
    public async Task Export_WithoutAuthentication_IsRejected()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/account/export");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesTheAccount_AndDeauthenticates()
    {
        var client = await RegisteredClientAsync("leaver");
        var token = await CsrfTokenAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/account/delete")
        {
            Content = JsonContent.Create(new { confirmUsername = "leaver" }),
        };
        request.Headers.Add("X-CSRF-TOKEN", token);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The session is gone …
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);

        // … and the account no longer exists (login now fails).
        var login = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new { usernameOrEmail = "leaver", password = "Password123!" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Delete_WithWrongConfirmation_IsRejected()
    {
        var client = await RegisteredClientAsync("careful");
        var token = await CsrfTokenAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/account/delete")
        {
            Content = JsonContent.Create(new { confirmUsername = "not-my-name" }),
        };
        request.Headers.Add("X-CSRF-TOKEN", token);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Still authenticated — nothing was deleted.
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }
}
