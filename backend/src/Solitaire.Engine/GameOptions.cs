namespace Solitaire.Engine;

/// <summary>
/// Configurable rules for a Klondike game. Immutable; part of the deterministic
/// contract (the same seed + options always yields the same game).
/// </summary>
/// <param name="DrawCount">
/// Number of cards flipped from stock to waste per draw. Must be 1 or 3.
/// </param>
/// <param name="MaxRedeals">
/// Maximum number of times the waste may be recycled back into the stock.
/// <see cref="Unlimited"/> (the default) means no limit.
/// </param>
public readonly record struct GameOptions(int DrawCount, int MaxRedeals)
{
    /// <summary>Sentinel for an unlimited number of redeals.</summary>
    public const int Unlimited = int.MaxValue;

    /// <summary>Draw one card at a time, unlimited redeals.</summary>
    public static GameOptions DrawOne { get; } = new(1, Unlimited);

    /// <summary>Draw three cards at a time, unlimited redeals.</summary>
    public static GameOptions DrawThree { get; } = new(3, Unlimited);

    /// <summary>Throws if the options are not a legal Klondike configuration.</summary>
    public void Validate()
    {
        if (DrawCount is not (1 or 3))
        {
            throw new ArgumentOutOfRangeException(
                nameof(DrawCount), DrawCount, "DrawCount must be 1 or 3.");
        }

        if (MaxRedeals < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxRedeals), MaxRedeals, "MaxRedeals must be >= 0 (or GameOptions.Unlimited).");
        }
    }
}
