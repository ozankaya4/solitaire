namespace Solitaire.Engine;

/// <summary>
/// The Pyramid scoring model. Simple and strictly non-negative (there is no
/// redeal penalty — the user's chosen rules make redeals unlimited and free —
/// so unlike Klondike/Spider there is no clamping to do).
/// </summary>
public static class PyramidScoring
{
    /// <summary>Removing a pair (two cards summing to 13) clears two cards at once.</summary>
    public const int RemovePair = 15;

    /// <summary>Removing a lone King clears one card.</summary>
    public const int RemoveSingle = 10;
}
