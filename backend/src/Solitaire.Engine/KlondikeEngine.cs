namespace Solitaire.Engine;

/// <summary>
/// <see cref="ISolitaireEngine"/> adapter for Klondike. Thin wrapper over the
/// static <see cref="Klondike"/> methods that presents the variant-neutral,
/// verification-focused surface the API consumes.
/// </summary>
public sealed class KlondikeEngine : ISolitaireEngine
{
    /// <inheritdoc />
    public string Variant => "klondike";

    /// <inheritdoc />
    public ReplayOutcome Replay(GameDefinition game)
    {
        ArgumentNullException.ThrowIfNull(game);

        var options = Klondike.OptionsFromBag(game.Options);
        var moves = game.Moves.Select(Klondike.FromDto).ToList();
        return Klondike.Replay(game.Seed, options, moves).ToOutcome();
    }
}
