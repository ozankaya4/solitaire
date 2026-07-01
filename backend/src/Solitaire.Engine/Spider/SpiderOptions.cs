namespace Solitaire.Engine;

/// <summary>
/// Configurable rules for a Spider game. Immutable; part of the deterministic
/// contract. Spider always uses two full 52-card decks (104 cards); the
/// difficulty is set by how many distinct suits those cards span.
/// </summary>
/// <param name="SuitCount">
/// Number of distinct suits in play: 1 (easy), 2 (medium) or 4 (hard).
/// 1-suit uses 8 copies of each spade; 2-suit uses 4 copies each of spades and
/// hearts; 4-suit uses 2 copies of a normal deck.
/// </param>
public readonly record struct SpiderOptions(int SuitCount)
{
    /// <summary>One suit (spades only) — easiest.</summary>
    public static SpiderOptions OneSuit { get; } = new(1);

    /// <summary>Two suits (spades + hearts).</summary>
    public static SpiderOptions TwoSuit { get; } = new(2);

    /// <summary>Four suits — hardest.</summary>
    public static SpiderOptions FourSuit { get; } = new(4);

    /// <summary>Throws if the options are not a legal Spider configuration.</summary>
    public void Validate()
    {
        if (SuitCount is not (1 or 2 or 4))
        {
            throw new ArgumentOutOfRangeException(
                nameof(SuitCount), SuitCount, "Spider SuitCount must be 1, 2, or 4.");
        }
    }
}
