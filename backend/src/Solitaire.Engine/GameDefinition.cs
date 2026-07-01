namespace Solitaire.Engine;

/// <summary>
/// A fully portable description of a game to (re)play: the seed, the rule options
/// as a simple string→int bag, and the ordered moves. This is the variant-neutral
/// input the API hands to <see cref="ISolitaireEngine.Replay"/> for verification;
/// it maps one-to-one onto an entry in a shared vectors file.
/// </summary>
/// <param name="Seed">Seed for the deterministic deal.</param>
/// <param name="Options">
/// Rule options (e.g. Klondike: <c>drawCount</c>, <c>maxRedeals</c>; Spider:
/// <c>suitCount</c>). Each engine reads the keys it needs and validates them.
/// </param>
/// <param name="Moves">The move sequence to replay, in order.</param>
public sealed record GameDefinition(
    int Seed,
    IReadOnlyDictionary<string, int> Options,
    IReadOnlyList<MoveDto> Moves);
