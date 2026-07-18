namespace Solitaire.Engine;

/// <summary>
/// <see cref="ISolitaireEngine"/> adapter for TriPeaks. Thin wrapper over the
/// static <see cref="TriPeaks"/> methods that presents the variant-neutral,
/// verification-focused surface the API consumes.
/// </summary>
public sealed class TriPeaksEngine : ISolitaireEngine
{
    /// <inheritdoc />
    public string Variant => "tripeaks";

    /// <inheritdoc />
    public ReplayOutcome Replay(GameDefinition game)
    {
        ArgumentNullException.ThrowIfNull(game);

        TriPeaks.OptionsFromBag(game.Options); // validated for parity; TriPeaks ignores the bag
        var moves = game.Moves.Select(TriPeaks.FromDto).ToList();
        return TriPeaks.Replay(game.Seed, moves).ToOutcome();
    }
}
