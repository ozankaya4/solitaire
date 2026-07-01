namespace Solitaire.Engine;

/// <summary>
/// The documented Spider scoring model (the classic Microsoft Spider model).
/// </summary>
/// <remarks>
/// You start at <see cref="InitialScore"/> = 500. Every player move (a deal or a
/// tableau-to-tableau move) costs <see cref="MovePenalty"/> = 1 point. Completing
/// a full King→Ace suit sequence earns <see cref="CompletedSequenceBonus"/> = 100.
/// The running score is clamped so it never drops below zero. There is no time
/// bonus, so the score is a pure function of the move sequence.
/// </remarks>
public static class SpiderScoring
{
    public const int InitialScore = 500;
    public const int MovePenalty = -1;
    public const int CompletedSequenceBonus = 100;

    /// <summary>Clamps a running score to a minimum of zero.</summary>
    public static int Clamp(int score) => score < 0 ? 0 : score;
}
