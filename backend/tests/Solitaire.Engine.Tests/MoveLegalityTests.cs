using Xunit;
using static Solitaire.Engine.Tests.TestStates;

namespace Solitaire.Engine.Tests;

public class MoveLegalityTests
{
    // ---- Draw -------------------------------------------------------------

    [Fact]
    public void Draw_MovesCardsFromStockToWaste_TopIsLastDrawn()
    {
        var stock = new[] { Card(Suit.Clubs, 5), Card(Suit.Hearts, 9), Card(Suit.Spades, 2) };
        var state = State(options: GameOptions.DrawOne, stock: stock);

        Assert.True(Klondike.TryApplyMove(state, Move.Draw(), out var next, out int delta));
        Assert.Equal(0, delta);
        Assert.Equal(2, next.Stock.Length);
        Assert.Equal(Card(Suit.Clubs, 5), next.WasteTop); // stock top became waste top
    }

    [Fact]
    public void DrawThree_MovesThreeCards_DeepestDrawnBecomesTop()
    {
        var stock = new[] { Card(Suit.Clubs, 5), Card(Suit.Hearts, 9), Card(Suit.Spades, 2), Card(Suit.Diamonds, 4) };
        var state = State(options: GameOptions.DrawThree, stock: stock);

        Assert.True(Klondike.TryApplyMove(state, Move.Draw(), out var next, out _));
        Assert.Single(next.Stock);
        Assert.Equal(3, next.Waste.Length);
        Assert.Equal(Card(Suit.Spades, 2), next.WasteTop); // third card drawn is on top
    }

    [Fact]
    public void DrawThree_WithFewerThanThreeInStock_DrawsWhatRemains()
    {
        var state = State(options: GameOptions.DrawThree, stock: [Card(Suit.Clubs, 5), Card(Suit.Hearts, 9)]);

        Assert.True(Klondike.TryApplyMove(state, Move.Draw(), out var next, out _));
        Assert.True(next.Stock.IsEmpty);
        Assert.Equal(2, next.Waste.Length);
    }

    [Fact]
    public void Draw_WithEmptyStock_IsIllegal()
    {
        var state = State(waste: [Card(Suit.Clubs, 5)]);
        Assert.False(Klondike.TryApplyMove(state, Move.Draw(), out _, out _));
    }

    // ---- Recycle ----------------------------------------------------------

    [Fact]
    public void Recycle_RestoresOriginalDrawOrder()
    {
        // Simulate a state where three cards were drawn (waste bottom->top).
        var waste = new[] { Card(Suit.Clubs, 5), Card(Suit.Hearts, 9), Card(Suit.Spades, 2) };
        var state = State(options: GameOptions.DrawOne, waste: waste);

        Assert.True(Klondike.TryApplyMove(state, Move.Recycle(), out var next, out _));
        Assert.True(next.Waste.IsEmpty);
        Assert.Equal(3, next.Stock.Length);
        Assert.Equal(1, next.RedealsUsed);
        // Drawing again yields the first-drawn card first.
        Assert.True(Klondike.TryApplyMove(next, Move.Draw(), out var afterDraw, out _));
        Assert.Equal(Card(Suit.Clubs, 5), afterDraw.WasteTop);
    }

    [Fact]
    public void Recycle_WithNonEmptyStock_IsIllegal()
    {
        var state = State(stock: [Card(Suit.Clubs, 5)], waste: [Card(Suit.Hearts, 9)]);
        Assert.False(Klondike.TryApplyMove(state, Move.Recycle(), out _, out _));
    }

    [Fact]
    public void Recycle_WithEmptyWaste_IsIllegal()
    {
        var state = State();
        Assert.False(Klondike.TryApplyMove(state, Move.Recycle(), out _, out _));
    }

    [Fact]
    public void Recycle_BeyondMaxRedeals_IsIllegal()
    {
        var options = new GameOptions(1, MaxRedeals: 1);
        var state = State(options: options, waste: [Card(Suit.Hearts, 9)], redealsUsed: 1);
        Assert.False(Klondike.TryApplyMove(state, Move.Recycle(), out _, out _));
    }

    // ---- Waste -> Foundation ---------------------------------------------

    [Fact]
    public void WasteToFoundation_AcceptsAceOntoEmptyFoundation()
    {
        var state = State(waste: [Card(Suit.Hearts, 1)]);
        Assert.True(Klondike.TryApplyMove(state, Move.WasteToFoundation(), out var next, out int delta));
        Assert.Equal(Scoring.WasteToFoundation, delta);
        Assert.Equal(1, next.Foundations[(int)Suit.Hearts]);
        Assert.True(next.Waste.IsEmpty);
    }

    [Fact]
    public void WasteToFoundation_RejectsWrongRank()
    {
        var state = State(waste: [Card(Suit.Hearts, 3)]); // foundation empty, needs Ace
        Assert.False(Klondike.TryApplyMove(state, Move.WasteToFoundation(), out _, out _));
    }

    [Fact]
    public void WasteToFoundation_AcceptsNextRankSameSuit()
    {
        var state = State(waste: [Card(Suit.Hearts, 3)], foundations: [0, 0, 2, 0]);
        Assert.True(Klondike.TryApplyMove(state, Move.WasteToFoundation(), out var next, out _));
        Assert.Equal(3, next.Foundations[(int)Suit.Hearts]);
    }

    // ---- Waste -> Tableau -------------------------------------------------

    [Fact]
    public void WasteToTableau_KingOntoEmptyPileIsLegal()
    {
        var state = State(waste: [Card(Suit.Spades, 13)]);
        Assert.True(Klondike.TryApplyMove(state, Move.WasteToTableau(0), out var next, out int delta));
        Assert.Equal(Scoring.WasteToTableau, delta);
        Assert.Equal(Card(Suit.Spades, 13), next.Tableau[0].TopCard);
    }

    [Fact]
    public void WasteToTableau_NonKingOntoEmptyPileIsIllegal()
    {
        var state = State(waste: [Card(Suit.Spades, 12)]);
        Assert.False(Klondike.TryApplyMove(state, Move.WasteToTableau(0), out _, out _));
    }

    [Fact]
    public void WasteToTableau_AcceptsDescendingAlternatingColor()
    {
        // Red 9 onto black 10.
        var tableau = new[] { FaceUp(Card(Suit.Spades, 10)) };
        var state = State(waste: [Card(Suit.Hearts, 9)], tableau: tableau);
        Assert.True(Klondike.TryApplyMove(state, Move.WasteToTableau(0), out var next, out _));
        Assert.Equal(Card(Suit.Hearts, 9), next.Tableau[0].TopCard);
    }

    [Fact]
    public void WasteToTableau_RejectsSameColor()
    {
        // Black 9 onto black 10 is illegal.
        var tableau = new[] { FaceUp(Card(Suit.Spades, 10)) };
        var state = State(waste: [Card(Suit.Clubs, 9)], tableau: tableau);
        Assert.False(Klondike.TryApplyMove(state, Move.WasteToTableau(0), out _, out _));
    }

    [Fact]
    public void WasteToTableau_RejectsWrongRank()
    {
        var tableau = new[] { FaceUp(Card(Suit.Spades, 10)) };
        var state = State(waste: [Card(Suit.Hearts, 8)], tableau: tableau);
        Assert.False(Klondike.TryApplyMove(state, Move.WasteToTableau(0), out _, out _));
    }

    // ---- Tableau -> Foundation -------------------------------------------

    [Fact]
    public void TableauToFoundation_MovesTopCardAndFlipsHiddenCard()
    {
        // Pile: [face-down 5C][face-up AH]. Moving AH to foundation flips 5C.
        var tableau = new[] { Pile(1, Card(Suit.Clubs, 5), Card(Suit.Hearts, 1)) };
        var state = State(tableau: tableau);

        Assert.True(Klondike.TryApplyMove(state, Move.TableauToFoundation(0), out var next, out int delta));
        Assert.Equal(Scoring.TableauToFoundation + Scoring.TurnOverTableauCard, delta);
        Assert.Equal(1, next.Foundations[(int)Suit.Hearts]);
        Assert.Equal(1, next.Tableau[0].Count);
        Assert.Equal(0, next.Tableau[0].FaceDownCount); // 5C is now face-up
    }

    [Fact]
    public void TableauToFoundation_RejectsIllegalRank()
    {
        var tableau = new[] { FaceUp(Card(Suit.Hearts, 5)) }; // foundation empty
        var state = State(tableau: tableau);
        Assert.False(Klondike.TryApplyMove(state, Move.TableauToFoundation(0), out _, out _));
    }

    // ---- Foundation -> Tableau -------------------------------------------

    [Fact]
    public void FoundationToTableau_MovesTopFoundationCardDown()
    {
        // Hearts foundation up to 3; move 3H onto black 4.
        var tableau = new[] { FaceUp(Card(Suit.Spades, 4)) };
        var state = State(foundations: [0, 0, 3, 0], tableau: tableau);

        Assert.True(Klondike.TryApplyMove(
            state, Move.FoundationToTableau(Suit.Hearts, 0), out var next, out int delta));
        Assert.Equal(Scoring.FoundationToTableau, delta);
        Assert.Equal(2, next.Foundations[(int)Suit.Hearts]);
        Assert.Equal(Card(Suit.Hearts, 3), next.Tableau[0].TopCard);
    }

    [Fact]
    public void FoundationToTableau_FromEmptyFoundation_IsIllegal()
    {
        var tableau = new[] { FaceUp(Card(Suit.Spades, 4)) };
        var state = State(foundations: [0, 0, 0, 0], tableau: tableau);
        Assert.False(Klondike.TryApplyMove(
            state, Move.FoundationToTableau(Suit.Hearts, 0), out _, out _));
    }

    // ---- Tableau -> Tableau ----------------------------------------------

    [Fact]
    public void TableauToTableau_MovesValidRunAndFlipsSource()
    {
        // Source: [down 2C][up 8H, 7S]; dest: [up 9S]. Move run (8H,7S) onto 9S.
        var source = Pile(1, Card(Suit.Clubs, 2), Card(Suit.Hearts, 8), Card(Suit.Spades, 7));
        var dest = FaceUp(Card(Suit.Spades, 9));
        var state = State(tableau: [source, dest]);

        Assert.True(Klondike.TryApplyMove(
            state, Move.TableauToTableau(0, 1, 2), out var next, out int delta));
        Assert.Equal(Scoring.TurnOverTableauCard, delta); // 2C flipped
        Assert.Equal(1, next.Tableau[0].Count);
        Assert.Equal(0, next.Tableau[0].FaceDownCount);
        Assert.Equal(3, next.Tableau[1].Count);
        Assert.Equal(Card(Suit.Spades, 7), next.Tableau[1].TopCard);
    }

    [Fact]
    public void TableauToTableau_RejectsMoreCardsThanFaceUp()
    {
        var source = Pile(1, Card(Suit.Clubs, 2), Card(Suit.Hearts, 8));
        var dest = FaceUp(Card(Suit.Spades, 9));
        var state = State(tableau: [source, dest]);
        // Only 1 face-up card, but ask to move 2.
        Assert.False(Klondike.TryApplyMove(state, Move.TableauToTableau(0, 1, 2), out _, out _));
    }

    [Fact]
    public void TableauToTableau_RejectsWhenBottomCardDoesNotFitDestination()
    {
        // Run bottom is 8H (red); dest top is 9H (red) — same color, illegal.
        var source = FaceUp(Card(Suit.Hearts, 8));
        var dest = FaceUp(Card(Suit.Hearts, 9));
        var state = State(tableau: [source, dest]);
        Assert.False(Klondike.TryApplyMove(state, Move.TableauToTableau(0, 1, 1), out _, out _));
    }

    [Fact]
    public void TableauToTableau_KingRunOntoEmptyColumnIsLegal()
    {
        // Source: [down 4D][up KS, QH]; dest empty. Move (KS,QH) to empty column.
        var source = Pile(1, Card(Suit.Diamonds, 4), Card(Suit.Spades, 13), Card(Suit.Hearts, 12));
        var state = State(tableau: [source, TableauPile.Empty]);

        Assert.True(Klondike.TryApplyMove(
            state, Move.TableauToTableau(0, 1, 2), out var next, out int delta));
        Assert.Equal(Scoring.TurnOverTableauCard, delta);
        Assert.Equal(2, next.Tableau[1].Count);
        Assert.Equal(Card(Suit.Spades, 13), next.Tableau[1].Cards[0]);
    }

    [Fact]
    public void TableauToTableau_SameSourceAndDestination_IsIllegal()
    {
        var source = FaceUp(Card(Suit.Spades, 13));
        var state = State(tableau: [source]);
        Assert.False(Klondike.TryApplyMove(state, Move.TableauToTableau(0, 0, 1), out _, out _));
    }

    // ---- No mutation on failure ------------------------------------------

    [Fact]
    public void FailedMove_ReturnsUnchangedStateAndZeroDelta()
    {
        var state = State(waste: [Card(Suit.Hearts, 3)]); // illegal waste->foundation
        Assert.False(Klondike.TryApplyMove(state, Move.WasteToFoundation(), out var next, out int delta));
        Assert.Same(state, next);
        Assert.Equal(0, delta);
    }
}
