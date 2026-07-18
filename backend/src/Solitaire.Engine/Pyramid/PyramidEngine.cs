namespace Solitaire.Engine;

/// <summary>
/// <see cref="ISolitaireEngine"/> adapter for Pyramid. Thin wrapper over the
/// static <see cref="Pyramid"/> methods that presents the variant-neutral,
/// verification-focused surface the API consumes.
/// </summary>
public sealed class PyramidEngine : ISolitaireEngine
{
    /// <inheritdoc />
    public string Variant => "pyramid";

    /// <inheritdoc />
    public ReplayOutcome Replay(GameDefinition game)
    {
        ArgumentNullException.ThrowIfNull(game);

        Pyramid.OptionsFromBag(game.Options); // validated for parity; Pyramid ignores the bag
        var moves = game.Moves.Select(Pyramid.FromDto).ToList();
        return Pyramid.Replay(game.Seed, moves).ToOutcome();
    }
}
