namespace Solitaire.Engine;

/// <summary>
/// <see cref="ISolitaireEngine"/> adapter for FreeCell. Thin wrapper over the
/// static <see cref="FreeCell"/> methods that presents the variant-neutral,
/// verification-focused surface the API consumes.
/// </summary>
public sealed class FreeCellEngine : ISolitaireEngine
{
    /// <inheritdoc />
    public string Variant => "freecell";

    /// <inheritdoc />
    public ReplayOutcome Replay(GameDefinition game)
    {
        ArgumentNullException.ThrowIfNull(game);

        FreeCell.OptionsFromBag(game.Options); // validated for parity; FreeCell ignores the bag
        var moves = game.Moves.Select(FreeCell.FromDto).ToList();
        return FreeCell.Replay(game.Seed, moves).ToOutcome();
    }
}
