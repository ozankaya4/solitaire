namespace Solitaire.Engine;

/// <summary>
/// The TriPeaks scoring model. Simple and strictly non-negative (there is no
/// redeal penalty — the user's chosen rules make redeals unlimited and free —
/// so unlike Klondike/Spider there is no clamping to do).
/// </summary>
public static class TriPeaksScoring
{
    /// <summary>Playing a tableau card onto the waste.</summary>
    public const int PlayToWaste = 10;
}
