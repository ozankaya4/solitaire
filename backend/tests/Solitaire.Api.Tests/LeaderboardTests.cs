using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Solitaire.Api.Tests;

public class LeaderboardTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory = factory;

    // ---- helpers ------------------------------------------------------------

    private sealed record VectorMove(string Type, int? Source, int? Destination, int? Count);

    private sealed record Vector(
        string Name,
        string Variant,
        int Seed,
        Dictionary<string, int> Options,
        List<VectorMove> Moves,
        int ExpectedFinalScore,
        bool ExpectedWin);

    private sealed record VectorFile(string Description, List<Vector> Vectors);

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    /// <summary>Loads the verified Klondike win from the shared cross-language vectors.</summary>
    private static Vector LoadWinVector()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "shared")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);

        string json = File.ReadAllText(Path.Combine(dir.FullName, "shared", "vectors", "klondike.json"));
        var file = JsonSerializer.Deserialize<VectorFile>(json, WebJson);
        Assert.NotNull(file);

        var vector = file.Vectors.Single(v => v.Name == "draw1-seed2-win");
        Assert.True(vector.ExpectedWin);
        return vector;
    }

    private static object SubmissionFor(Vector vector, int? seedOverride = null, long? timeMs = null) => new
    {
        variant = vector.Variant,
        seed = seedOverride ?? vector.Seed,
        options = vector.Options,
        moves = vector.Moves.Select(m => new { type = m.Type, source = m.Source, destination = m.Destination, count = m.Count }),
        claimedScore = vector.ExpectedFinalScore,
        // Comfortably plausible: 500 ms per move.
        claimedTimeMs = timeMs ?? vector.Moves.Count * 500L,
    };

    private async Task<HttpClient> RegisteredClientAsync(string name)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { username = name, email = $"{name}@example.com", password = "Password123!" });
        response.EnsureSuccessStatusCode();
        return client; // carries the auth cookie
    }

    // ---- the required tests --------------------------------------------------

    [Fact]
    public async Task LegitimateGame_IsAccepted_AndRanked()
    {
        var vector = LoadWinVector();
        var client = await RegisteredClientAsync("honest_player");

        var response = await client.PostAsJsonAsync("/api/games/submit", SubmissionFor(vector));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(vector.ExpectedFinalScore, body.GetProperty("score").GetInt32());
        Assert.True(body.GetProperty("rank").GetInt32() >= 1);

        // The verified score shows on the public leaderboard with the player's rank.
        var board = await client.GetFromJsonAsync<JsonElement>("/api/leaderboard/klondike");
        var top = board.GetProperty("top").EnumerateArray().ToList();
        Assert.Contains(top, row =>
            row.GetProperty("username").GetString() == "honest_player" &&
            row.GetProperty("score").GetInt32() == vector.ExpectedFinalScore);
        Assert.True(board.GetProperty("playerRank").GetInt32() >= 1);
    }

    [Fact]
    public async Task TamperedMoveLog_IsRejected_AndNotRecorded()
    {
        var vector = LoadWinVector();
        var client = await RegisteredClientAsync("tamperer");

        // Cut the tail off the winning line: every move is legal but it is no
        // longer a win — the claimed score/win is a lie the replay exposes.
        var tampered = vector with { Moves = vector.Moves.Take(vector.Moves.Count - 5).ToList() };

        var response = await client.PostAsJsonAsync("/api/games/submit", SubmissionFor(tampered));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        // And a log whose *content* was edited mid-sequence trips the legality check.
        var reordered = vector with { Moves = [.. vector.Moves] };
        (reordered.Moves[0], reordered.Moves[^1]) = (reordered.Moves[^1], reordered.Moves[0]);
        var response2 = await client.PostAsJsonAsync("/api/games/submit", SubmissionFor(reordered));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response2.StatusCode);

        // Nothing was recorded for this player.
        var board = await client.GetFromJsonAsync<JsonElement>("/api/leaderboard/klondike");
        Assert.Equal(JsonValueKind.Null, board.GetProperty("playerRank").ValueKind);
    }

    [Fact]
    public async Task ForgedScore_WithWrongSeed_IsRejected()
    {
        var vector = LoadWinVector();
        var client = await RegisteredClientAsync("forger");

        // A valid-looking move log, but replayed against a different deal it is
        // not the same game — the engine replay fails it.
        var response = await client.PostAsJsonAsync(
            "/api/games/submit", SubmissionFor(vector, seedOverride: vector.Seed + 1));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedSubmission_IsRejected()
    {
        var vector = LoadWinVector();
        var client = _factory.CreateClient(); // no cookie — a guest

        var response = await client.PostAsJsonAsync("/api/games/submit", SubmissionFor(vector));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- additional guards ----------------------------------------------------

    [Fact]
    public async Task DuplicateSubmission_IsRejectedAsConflict()
    {
        var vector = LoadWinVector();
        var client = await RegisteredClientAsync("repeater");

        var first = await client.PostAsJsonAsync("/api/games/submit", SubmissionFor(vector));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/games/submit", SubmissionFor(vector));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task ImplausiblyFastGame_IsRejected()
    {
        var vector = LoadWinVector();
        var client = await RegisteredClientAsync("speedster");

        // Claims the whole game took 1 second — faster than the minimum pace.
        var response = await client.PostAsJsonAsync(
            "/api/games/submit", SubmissionFor(vector, timeMs: 1000));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
