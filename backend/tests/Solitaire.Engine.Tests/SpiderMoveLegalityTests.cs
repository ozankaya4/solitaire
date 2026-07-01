using Xunit;
using static Solitaire.Engine.Tests.SpiderTestStates;

namespace Solitaire.Engine.Tests;

public class SpiderMoveLegalityTests
{
    // Builds ten single-card columns so dealing is legal.
    private static TableauPile[] TenColumns()
    {
        var piles = new TableauPile[10];
        for (int i = 0; i < 10; i++)
        {
            piles[i] = FaceUp(Card(Suit.Spades, 5));
        }

        return piles;
    }

    // ---- Deal -------------------------------------------------------------

    [Fact]
    public void Deal_DealsOneCardToEachColumn()
    {
        var stock = Enumerable.Range(0, 10).Select(i => Card(Suit.Spades, (i % 13) + 1)).ToArray();
        var state = State(tableau: TenColumns(), stock: stock);

        Assert.True(Spider.TryApplyMove(state, SpiderMove.Deal(), out var next, out int delta));
        Assert.Equal(SpiderScoring.MovePenalty, delta);
        Assert.True(next.Stock.IsEmpty);
        for (int p = 0; p < 10; p++)
        {
            Assert.Equal(2, next.Tableau[p].Count);
            Assert.Equal(stock[p], next.Tableau[p].TopCard);
        }
    }

    [Fact]
    public void Deal_WithAnEmptyColumn_IsIllegal()
    {
        var piles = TenColumns();
        piles[3] = TableauPile.Empty;
        var state = State(tableau: piles, stock: Enumerable.Repeat(Card(Suit.Spades, 4), 10));
        Assert.False(Spider.TryApplyMove(state, SpiderMove.Deal(), out _, out _));
    }

    [Fact]
    public void Deal_WithEmptyStock_IsIllegal()
    {
        var state = State(tableau: TenColumns());
        Assert.False(Spider.TryApplyMove(state, SpiderMove.Deal(), out _, out _));
    }

    // ---- Tableau -> Tableau ----------------------------------------------

    [Fact]
    public void SingleCard_OntoRankPlusOne_AnySuit_IsLegal()
    {
        // Move a red 8 onto a black 9 — suit is irrelevant for a single card.
        var source = FaceUp(Card(Suit.Hearts, 8));
        var dest = FaceUp(Card(Suit.Spades, 9));
        var state = State(options: SpiderOptions.FourSuit, tableau: [source, dest]);

        Assert.True(Spider.TryApplyMove(state, SpiderMove.TableauToTableau(0, 1, 1), out var next, out _));
        Assert.Equal(Card(Suit.Hearts, 8), next.Tableau[1].TopCard);
    }

    [Fact]
    public void SameSuitDescendingRun_MovesTogether()
    {
        // Source top run 9-8-7 spades; dest top is 10 spades.
        var source = FaceUp(Card(Suit.Spades, 9), Card(Suit.Spades, 8), Card(Suit.Spades, 7));
        var dest = FaceUp(Card(Suit.Spades, 10));
        var state = State(tableau: [source, dest]);

        Assert.True(Spider.TryApplyMove(state, SpiderMove.TableauToTableau(0, 1, 3), out var next, out _));
        Assert.Equal(4, next.Tableau[1].Count);
        Assert.True(next.Tableau[0].IsEmpty);
    }

    [Fact]
    public void MixedSuitRun_CannotMoveTogether()
    {
        // Top two cards 9S, 8H are descending but different suits -> not a movable run of 2.
        var source = FaceUp(Card(Suit.Spades, 9), Card(Suit.Hearts, 8));
        var dest = FaceUp(Card(Suit.Spades, 10));
        var state = State(options: SpiderOptions.FourSuit, tableau: [source, dest]);
        Assert.False(Spider.TryApplyMove(state, SpiderMove.TableauToTableau(0, 1, 2), out _, out _));
    }

    [Fact]
    public void MovingMoreThanTheInSuitRun_IsIllegal()
    {
        var source = FaceUp(Card(Suit.Spades, 9), Card(Suit.Spades, 7)); // not consecutive
        var dest = FaceUp(Card(Suit.Spades, 10));
        var state = State(tableau: [source, dest]);
        Assert.False(Spider.TryApplyMove(state, SpiderMove.TableauToTableau(0, 1, 2), out _, out _));
    }

    [Fact]
    public void RunOntoEmptyColumn_IsLegal()
    {
        var source = Pile(1, Card(Suit.Spades, 13), Card(Suit.Spades, 5), Card(Suit.Spades, 4));
        var state = State(tableau: [source, TableauPile.Empty]);

        Assert.True(Spider.TryApplyMove(state, SpiderMove.TableauToTableau(0, 1, 2), out var next, out _));
        Assert.Equal(2, next.Tableau[1].Count);
        Assert.Equal(0, next.Tableau[0].FaceDownCount); // exposed card flipped up
    }

    [Fact]
    public void OntoWrongRank_IsIllegal()
    {
        var source = FaceUp(Card(Suit.Spades, 8));
        var dest = FaceUp(Card(Suit.Spades, 10)); // needs a 9, not an 8
        var state = State(tableau: [source, dest]);
        Assert.False(Spider.TryApplyMove(state, SpiderMove.TableauToTableau(0, 1, 1), out _, out _));
    }

    [Fact]
    public void SameSourceAndDestination_IsIllegal()
    {
        var state = State(tableau: [FaceUp(Card(Suit.Spades, 5))]);
        Assert.False(Spider.TryApplyMove(state, SpiderMove.TableauToTableau(0, 0, 1), out _, out _));
    }

    [Fact]
    public void FailedMove_ReturnsUnchangedStateAndZeroDelta()
    {
        var state = State(tableau: [FaceUp(Card(Suit.Spades, 8)), FaceUp(Card(Suit.Spades, 10))]);
        Assert.False(Spider.TryApplyMove(state, SpiderMove.TableauToTableau(0, 1, 1), out var next, out int delta));
        Assert.Same(state, next);
        Assert.Equal(0, delta);
    }

    // ---- Auto-completion --------------------------------------------------

    [Fact]
    public void CompletingKingToAce_RemovesSequenceAndScores()
    {
        // Dest holds K..2 (spades); move the Ace on to complete K..A.
        var dest = FaceUp(KingToAce(Suit.Spades)[..12]); // K down to 2
        var source = FaceUp(Card(Suit.Spades, 1));
        var state = State(tableau: [source, dest]);

        Assert.True(Spider.TryApplyMove(state, SpiderMove.TableauToTableau(0, 1, 1), out var next, out int delta));
        Assert.Equal(SpiderScoring.MovePenalty + SpiderScoring.CompletedSequenceBonus, delta); // -1 + 100
        Assert.Equal(1, next.CompletedSequences);
        Assert.True(next.Tableau[1].IsEmpty); // the finished sequence left the pile
    }

    [Fact]
    public void CompletingViaMultiCardRun_Works()
    {
        // Dest K..4 (spades, 10 cards); move run 3-2-A (spades) to complete.
        var dest = FaceUp(KingToAce(Suit.Spades)[..10]); // K..4
        var source = FaceUp(Card(Suit.Spades, 3), Card(Suit.Spades, 2), Card(Suit.Spades, 1));
        var state = State(tableau: [source, dest]);

        Assert.True(Spider.TryApplyMove(state, SpiderMove.TableauToTableau(0, 1, 3), out var next, out int delta));
        Assert.Equal(SpiderScoring.MovePenalty + SpiderScoring.CompletedSequenceBonus, delta);
        Assert.Equal(1, next.CompletedSequences);
    }
}
