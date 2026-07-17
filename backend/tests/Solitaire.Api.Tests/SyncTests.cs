using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Solitaire.Api.Tests;

/// <summary>
/// Cross-device sync of resumable games + progress. The important scenario is two
/// devices signed into one account: what one pushes, the other pulls.
/// </summary>
public class SyncTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory = factory;
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private sealed record VectorMove(string Type, int? Source, int? Destination, int? Count);
    private sealed record Vector(string Name, string Variant, int Seed, Dictionary<string, int> Options, List<VectorMove> Moves);
    private sealed record VectorFile(List<Vector> Vectors);

    /// <summary>A legal (partial, not-yet-won) Klondike game from the shared vectors.</summary>
    private static Vector LoadPartial()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "shared")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        string json = File.ReadAllText(Path.Combine(dir.FullName, "shared", "vectors", "klondike.json"));
        var file = JsonSerializer.Deserialize<VectorFile>(json, Web);
        Assert.NotNull(file);
        return file.Vectors.First(v => v.Name == "draw1-seed2-win");
    }

    private static object SaveBody(Vector v, int takeMoves, long updatedAt, int level = 31) => new
    {
        variant = v.Variant,
        level,
        seed = v.Seed,
        options = v.Options,
        moves = v.Moves.Take(takeMoves).Select(m => new { type = m.Type, source = m.Source, destination = m.Destination, count = m.Count }),
        hintsUsed = 0,
        elapsedMs = 45_000L,
        updatedAt,
    };

    private async Task<HttpClient> RegisterAsync(string name)
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/register",
            new { username = name, email = $"{name}@example.com", password = "Password123!" });
        res.EnsureSuccessStatusCode();
        return client;
    }

    private async Task<HttpClient> LoginAsync(string name)
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { usernameOrEmail = name, password = "Password123!", rememberMe = true });
        res.EnsureSuccessStatusCode();
        return client;
    }

    [Fact]
    public async Task PushedSave_IsVisibleOnAnotherDevice()
    {
        var v = LoadPartial();
        var deviceA = await RegisterAsync("syncer_a");

        var put = await deviceA.PutAsJsonAsync("/api/sync/saves/klondike", SaveBody(v, 12, 1000));
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        // A second device signs into the same account and pulls.
        var deviceB = await LoginAsync("syncer_a");
        var state = await deviceB.GetFromJsonAsync<JsonElement>("/api/sync");
        var saves = state.GetProperty("saves").EnumerateArray().ToList();
        Assert.Single(saves);
        Assert.Equal("klondike", saves[0].GetProperty("variant").GetString());
        Assert.Equal(31, saves[0].GetProperty("level").GetInt32());
        Assert.Equal(45_000, saves[0].GetProperty("elapsedMs").GetInt64());
    }

    [Fact]
    public async Task Progress_IsMonotonic_AcrossDevices()
    {
        var deviceA = await RegisterAsync("syncer_prog");
        Assert.Equal(HttpStatusCode.NoContent,
            (await deviceA.PutAsJsonAsync("/api/sync/progress/klondike", new { variant = "klondike", currentLevel = 7 })).StatusCode);

        // A stale device pushes an older, lower level — must NOT roll progress back.
        Assert.Equal(HttpStatusCode.NoContent,
            (await deviceA.PutAsJsonAsync("/api/sync/progress/klondike", new { variant = "klondike", currentLevel = 3 })).StatusCode);

        var deviceB = await LoginAsync("syncer_prog");
        var state = await deviceB.GetFromJsonAsync<JsonElement>("/api/sync");
        var prog = state.GetProperty("progress").EnumerateArray().Single();
        Assert.Equal(7, prog.GetProperty("currentLevel").GetInt32());
    }

    [Fact]
    public async Task NewerSave_Wins_OlderSave_IsIgnored()
    {
        var v = LoadPartial();
        var client = await RegisterAsync("syncer_conflict");

        // Newer save first (12 moves, t=5000), then an older push (4 moves, t=2000).
        await client.PutAsJsonAsync("/api/sync/saves/klondike", SaveBody(v, 12, 5000));
        await client.PutAsJsonAsync("/api/sync/saves/klondike", SaveBody(v, 4, 2000));

        var state = await client.GetFromJsonAsync<JsonElement>("/api/sync");
        var save = state.GetProperty("saves").EnumerateArray().Single();
        // The newer (12-move) save survived.
        Assert.Equal(12, save.GetProperty("moves").GetArrayLength());
    }

    [Fact]
    public async Task IllegalSave_IsRejected()
    {
        var v = LoadPartial();
        var client = await RegisterAsync("syncer_illegal");

        // Swap two moves so the sequence becomes illegal mid-way.
        var moves = v.Moves.Take(12).ToList();
        (moves[0], moves[11]) = (moves[11], moves[0]);
        var body = new
        {
            variant = "klondike",
            level = 31,
            seed = v.Seed,
            options = v.Options,
            moves = moves.Select(m => new { type = m.Type, source = m.Source, destination = m.Destination, count = m.Count }),
            hintsUsed = 0,
            elapsedMs = 1000L,
            updatedAt = 1000L,
        };
        var res = await client.PutAsJsonAsync("/api/sync/saves/klondike", body);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
    }

    [Fact]
    public async Task DeleteSave_RemovesIt()
    {
        var v = LoadPartial();
        var client = await RegisterAsync("syncer_delete");
        await client.PutAsJsonAsync("/api/sync/saves/klondike", SaveBody(v, 8, 1000));

        var del = await client.DeleteAsync("/api/sync/saves/klondike");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var state = await client.GetFromJsonAsync<JsonElement>("/api/sync");
        Assert.Empty(state.GetProperty("saves").EnumerateArray());
    }

    [Fact]
    public async Task Unauthenticated_IsRejected()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/sync");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
