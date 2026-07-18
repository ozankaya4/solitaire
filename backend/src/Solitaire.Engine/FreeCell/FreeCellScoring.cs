namespace Solitaire.Engine;

/// <summary>
/// The FreeCell scoring model. Simpler than Klondike's: every tableau card is
/// already face-up from the deal (no turn-over bonus is possible), and there is
/// no stock/waste/redeal. Only foundation progress is rewarded, with a small
/// penalty for undoing a foundation placement — the same shape as Klondike's
/// model, minus the concepts FreeCell doesn't have.
/// </summary>
public static class FreeCellScoring
{
    public const int ToFoundation = 10;
    public const int FoundationToTableau = -15;

    /// <summary>Clamps a running score to a minimum of zero.</summary>
    public static int Clamp(int score) => score < 0 ? 0 : score;
}
