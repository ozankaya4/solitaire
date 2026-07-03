using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Solitaire.Api.Tests;

public class LocalizationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory = factory;

    [Fact]
    public async Task LoginError_IsEnglish_ByDefault()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { usernameOrEmail = "ghost", password = "Whatever123!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Invalid username/email or password.", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task LoginError_IsTurkish_WithAcceptLanguage()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Accept-Language", "tr");

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { usernameOrEmail = "ghost", password = "Whatever123!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Kullanıcı adı/e-posta veya parola hatalı.", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task ValidationError_IsTurkish_WithAcceptLanguage()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Accept-Language", "tr");

        // Username too short → the localized length message.
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { username = "ab", email = "ab@example.com", password = "Password123!" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Kullanıcı adı 3–32 karakter olmalıdır.", body, StringComparison.Ordinal);
    }
}
