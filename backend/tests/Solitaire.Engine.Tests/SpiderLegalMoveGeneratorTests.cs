using Xunit;
using static Solitaire.Engine.Tests.SpiderTestStates;

namespace Solitaire.Engine.Tests;

public class SpiderLegalMoveGeneratorTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void EveryGeneratedMove_IsActuallyLegal(int suitCount)
    {
        var options = new SpiderOptions(suitCount);
        var state = Spider.NewGame(5, options);

        for (int step = 0; step < 30; step++)
        {
            var moves = Spider.GetLegalMoves(state);
            foreach (var move in moves)
            {
                Assert.True(
                    Spider.TryApplyMove(state, move, out _, out _),
                    $"Generator produced an illegal move {move.Type} (suits={suitCount}).");
            }

            if (moves.Count == 0)
            {
                break;
            }

            Spider.TryApplyMove(state, moves[0], out state!, out _);
        }
    }

    [Fact]
    public void FreshGame_OffersADeal()
    {
        var state = Spider.NewGame(1, SpiderOptions.OneSuit);
        Assert.Contains(Spider.GetLegalMoves(state), m => m.Type == SpiderMoveType.Deal);
    }

    [Fact]
    public void WithAnEmptyColumn_DoesNotOfferDeal()
    {
        var piles = new TableauPile[10];
        for (int i = 0; i < 10; i++)
        {
            piles[i] = FaceUp(Card(Suit.Spades, 5));
        }

        piles[0] = TableauPile.Empty;
        var state = State(tableau: piles, stock: Enumerable.Repeat(Card(Suit.Spades, 6), 10));

        Assert.DoesNotContain(Spider.GetLegalMoves(state), m => m.Type == SpiderMoveType.Deal);
    }

    [Fact]
    public void GeneratorSkipsRelocatingWholeColumnToEmpty()
    {
        // A lone card (no hidden cards) moved to an empty column makes no progress.
        var state = State(tableau: [FaceUp(Card(Suit.Spades, 5)), TableauPile.Empty]);
        Assert.DoesNotContain(Spider.GetLegalMoves(state), m => m.Type == SpiderMoveType.TableauToTableau);

        // …but it is still accepted if applied explicitly.
        Assert.True(Spider.TryApplyMove(state, SpiderMove.TableauToTableau(0, 1, 1), out _, out _));
    }
}
