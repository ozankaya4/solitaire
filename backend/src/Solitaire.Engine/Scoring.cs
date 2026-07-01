namespace Solitaire.Engine;

/// <summary>
/// The exact, documented Klondike scoring model implemented by this engine.
/// </summary>
/// <remarks>
/// This is a move-based model in the spirit of the classic "Standard" Windows
/// Solitaire scoring. There is deliberately <b>no time bonus</b> (the engine has
/// no notion of time) so that a score is a pure function of the move sequence.
///
/// <list type="table">
/// <item><term>Waste → Foundation</term><description>+10</description></item>
/// <item><term>Tableau → Foundation</term><description>+10</description></item>
/// <item><term>Waste → Tableau</term><description>+5</description></item>
/// <item><term>Turn over a tableau card (face-down → face-up)</term><description>+5</description></item>
/// <item><term>Foundation → Tableau</term><description>-15</description></item>
/// <item><term>Recycle waste → stock, draw-1</term><description>-100</description></item>
/// <item><term>Recycle waste → stock, draw-3</term><description>-20</description></item>
/// <item><term>Tableau → Tableau</term><description>0 (plus any turn-over bonus)</description></item>
/// <item><term>Draw</term><description>0</description></item>
/// </list>
///
/// The running score is clamped so it never drops below zero.
/// </remarks>
public static class Scoring
{
    public const int WasteToFoundation = 10;
    public const int TableauToFoundation = 10;
    public const int WasteToTableau = 5;
    public const int TurnOverTableauCard = 5;
    public const int FoundationToTableau = -15;
    public const int RecycleDrawOne = -100;
    public const int RecycleDrawThree = -20;

    /// <summary>Clamps a running score to a minimum of zero.</summary>
    public static int Clamp(int score) => score < 0 ? 0 : score;
}
