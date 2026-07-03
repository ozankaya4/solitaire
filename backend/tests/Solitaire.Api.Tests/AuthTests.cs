using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Solitaire.Api.Tests;

public class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory = factory;

    [Fact]
    public async Task Register_CreatesAccount_AndIssuesAuthCookie()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { username = "alice", email = "alice@example.com", password = "Password123!" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.Contains("solitaire.auth"));

        // The cookie authenticates subsequent requests on the same client.
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_Succeeds()
    {
        // Arrange: register a user (separate client, then discard its cookie).
        var registerClient = _factory.CreateClient();
        var register = await registerClient.PostAsJsonAsync(
            "/api/auth/register",
            new { username = "bob", email = "bob@example.com", password = "Password123!" });
        register.EnsureSuccessStatusCode();

        // Act: log in on a fresh client using the email.
        var loginClient = _factory.CreateClient();
        var login = await loginClient.PostAsJsonAsync(
            "/api/auth/login",
            new { usernameOrEmail = "bob@example.com", password = "Password123!" });

        // Assert.
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains(login.Headers.GetValues("Set-Cookie"), c => c.Contains("solitaire.auth"));

        var me = await loginClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_IsUnauthorized()
    {
        var registerClient = _factory.CreateClient();
        var register = await registerClient.PostAsJsonAsync(
            "/api/auth/register",
            new { username = "carol", email = "carol@example.com", password = "Password123!" });
        register.EnsureSuccessStatusCode();

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { usernameOrEmail = "carol", password = "WrongPassword1!" });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutAuthentication_IsRejected()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SecurityHeaders_ArePresentOnResponses()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.Equal("nosniff", string.Join("", response.Headers.GetValues("X-Content-Type-Options")));
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
    }
}
