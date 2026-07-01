using Xunit;
using static Solitaire.Engine.Tests.TestStates;

namespace Solitaire.Engine.Tests;

public class LegalMoveGeneratorTests
{
    [Fact]
    public void EveryGeneratedMove_IsActuallyLegal()
    {
        // Play a handful of games and check the generator never lies.
        foreach (int seed in new[] { 1, 2, 3, 10, 42 })
        {
            var state = Klondike.NewGame(seed, GameOptions.DrawThree);
            for (int step = 0; step < 40; step++)
            {
                var moves = Klondike.GetLegalMoves(state);
                foreach (var move in moves)
                {
                    Assert.True(
                        Klondike.TryApplyMove(state, move, out _, out _),
                        $"Generator produced an illegal move {move.Type} on seed {seed}.");
                }

                if (moves.Count == 0)
                {
                    break;
                }

                // Advance using the first move so we sample varied states.
                Klondike.TryApplyMove(state, moves[0], out state!, out _);
            }
        }
    }

    [Fact]
    public void FreshGame_OffersADraw()
    {
        var state = Klondike.NewGame(1, GameOptions.DrawOne);
        Assert.Contains(Klondike.GetLegalMoves(state), m => m.Type == MoveType.Draw);
    }

    [Fact]
    public void EmptyStockWithWaste_OffersRecycleNotDraw()
    {
        var state = State(waste: [Card(Suit.Hearts, 9)]);
        var moves = Klondike.GetLegalMoves(state);
        Assert.Contains(moves, m => m.Type == MoveType.Recycle);
        Assert.DoesNotContain(moves, m => m.Type == MoveType.Draw);
    }

    [Fact]
    public void GeneratorFindsWasteToFoundationAndTableauPlays()
    {
        // Waste top = AH; empty foundations; a black 2 on tableau accepts nothing here,
        // but AH -> foundation should be offered.
        var state = State(waste: [Card(Suit.Hearts, 1)]);
        var moves = Klondike.GetLegalMoves(state);
        Assert.Contains(moves, m => m.Type == MoveType.WasteToFoundation);
    }

    [Fact]
    public void GeneratorSkipsRelocatingWholeColumnToEmpty()
    {
        // Source is a lone King (all face-up, no hidden cards); dest empty.
        // Moving it is legal but pointless, so the generator must not suggest it.
        var state = State(tableau: [FaceUp(Card(Suit.Spades, 13)), TableauPile.Empty]);
        var moves = Klondike.GetLegalMoves(state);
        Assert.DoesNotContain(moves, m => m.Type == MoveType.TableauToTableau);

        // But it is still accepted if applied explicitly.
        Assert.True(Klondike.TryApplyMove(state, Move.TableauToTableau(0, 1, 1), out _, out _));
    }
}
