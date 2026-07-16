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

    // Curated Klondike ladder: level 31 uses seed 2, level 32 uses seed 4. These
    // are the levels the shared win vectors below correspond to.
    private const int Seed2Level = 31;
    private const int Seed4Level = 32;

    /// <summary>Loads a verified Klondike win from the shared cross-language vectors.</summary>
    private static Vector LoadWinVector(string name = "draw1-seed2-win")
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

        var vector = file.Vectors.Single(v => v.Name == name);
        Assert.True(vector.ExpectedWin);
        return vector;
    }

    private static object SubmissionFor(
        Vector vector, int level = Seed2Level, int? seedOverride = null, long? timeMs = null) => new
    {
        variant = vector.Variant,
        seed = seedOverride ?? vector.Seed,
        level,
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
        Assert.Equal(Seed2Level, body.GetProperty("level").GetInt32());
        Assert.Equal(vector.ExpectedFinalScore, body.GetProperty("score").GetInt32());
        Assert.True(body.GetProperty("rank").GetInt32() >= 1);

        // The verified level shows on the public leaderboard with the player's rank.
        var board = await client.GetFromJsonAsync<JsonElement>("/api/leaderboard/klondike");
        var top = board.GetProperty("top").EnumerateArray().ToList();
        Assert.Contains(top, row =>
            row.GetProperty("username").GetString() == "honest_player" &&
            row.GetProperty("level").GetInt32() == Seed2Level &&
            row.GetProperty("score").GetInt32() == vector.ExpectedFinalScore);
        Assert.True(board.GetProperty("playerRank").GetInt32() >= 1);
        Assert.Equal(Seed2Level, board.GetProperty("playerBestLevel").GetInt32());
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

    // ---- level integrity -------------------------------------------------------

    [Fact]
    public async Task MislabeledLevel_IsRejected_AndNotRecorded()
    {
        var vector = LoadWinVector(); // a genuine seed-2 win...
        var client = await RegisteredClientAsync("mislabeler");

        // ...but claimed as level 1, whose canonical seed is not 2. The win is real,
        // yet the level label is a lie — recording it would let an easy deal pose as
        // a hard level.
        var response = await client.PostAsJsonAsync("/api/games/submit", SubmissionFor(vector, level: 1));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var board = await client.GetFromJsonAsync<JsonElement>("/api/leaderboard/klondike");
        Assert.Equal(JsonValueKind.Null, board.GetProperty("playerRank").ValueKind);
    }

    [Fact]
    public async Task LevelBeyondCuratedLadder_IsNotRankable()
    {
        var vector = LoadWinVector();
        var client = await RegisteredClientAsync("overreacher");

        // Klondike is only rankable within its curated ladder; a genuine win claimed
        // as a level past the ladder cannot be tied to a canonical seed.
        var response = await client.PostAsJsonAsync("/api/games/submit", SubmissionFor(vector, level: 100_000));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task HigherLevel_RanksAboveLowerLevel()
    {
        var lower = LoadWinVector("draw1-seed2-win"); // level 31
        var higher = LoadWinVector("draw1-seed4-win"); // level 32

        var lowClient = await RegisteredClientAsync("low_level");
        var lowResp = await lowClient.PostAsJsonAsync("/api/games/submit", SubmissionFor(lower, level: Seed2Level));
        Assert.Equal(HttpStatusCode.OK, lowResp.StatusCode);

        var highClient = await RegisteredClientAsync("high_level");
        var highResp = await highClient.PostAsJsonAsync("/api/games/submit", SubmissionFor(higher, level: Seed4Level));
        Assert.Equal(HttpStatusCode.OK, highResp.StatusCode);

        // The higher level outranks the lower one regardless of score.
        var board = await highClient.GetFromJsonAsync<JsonElement>("/api/leaderboard/klondike");
        var top = board.GetProperty("top").EnumerateArray().ToList();
        int highIndex = top.FindIndex(r => r.GetProperty("username").GetString() == "high_level");
        int lowIndex = top.FindIndex(r => r.GetProperty("username").GetString() == "low_level");
        Assert.True(highIndex >= 0 && lowIndex >= 0);
        Assert.True(highIndex < lowIndex);
        Assert.Equal(1, top[highIndex].GetProperty("rank").GetInt32());
    }
}
