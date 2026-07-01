namespace Solitaire.Engine;

/// <summary>
/// <see cref="ISolitaireEngine"/> adapter for Spider — the variant-neutral,
/// verification-focused surface the API consumes. Thin wrapper over <see cref="Spider"/>.
/// </summary>
public sealed class SpiderEngine : ISolitaireEngine
{
    /// <inheritdoc />
    public string Variant => "spider";

    /// <inheritdoc />
    public ReplayOutcome Replay(GameDefinition game)
    {
        ArgumentNullException.ThrowIfNull(game);

        var options = Spider.OptionsFromBag(game.Options);
        var moves = game.Moves.Select(Spider.FromDto).ToList();
        return Spider.Replay(game.Seed, options, moves).ToOutcome();
    }
}
