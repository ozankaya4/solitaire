using System.Collections.Immutable;

namespace Solitaire.Engine;

/// <summary>
/// Deterministic Spider deck construction and deal. Reuses the shared seeded
/// shuffle (<see cref="Deck.ShuffleInPlace{T}"/>) so the algorithm matches the
/// TypeScript port exactly.
/// </summary>
public static class SpiderDeck
{
    /// <summary>Spider uses two full decks: 104 cards.</summary>
    public const int Size = 104;

    /// <summary>Cards dealt to the ten tableau columns at the start (4×6 + 6×5).</summary>
    public const int TableauDealCount = 54;

    /// <summary>Cards per column at deal time: the first four columns get 6, the rest 5.</summary>
    public static int ColumnDealSize(int column) => column < 4 ? 6 : 5;

    /// <summary>
    /// The canonical unshuffled 104-card deck for a suit count. Suits used:
    /// 1 → Spades; 2 → Spades, Hearts; 4 → Clubs, Diamonds, Hearts, Spades. Each
    /// suit appears <c>104 / (13 * suitCount)</c> times. Order is copy-major, then
    /// suit (in the order listed), then rank 1..13 — part of the deterministic
    /// contract.
    /// </summary>
    public static ImmutableArray<Card> BuildOrdered(int suitCount)
    {
        Suit[] suits = suitCount switch
        {
            1 => [Suit.Spades],
            2 => [Suit.Spades, Suit.Hearts],
            4 => [Suit.Clubs, Suit.Diamonds, Suit.Hearts, Suit.Spades],
            _ => throw new ArgumentOutOfRangeException(nameof(suitCount), suitCount, "SuitCount must be 1, 2, or 4."),
        };

        int copies = Size / (13 * suitCount);
        var builder = ImmutableArray.CreateBuilder<Card>(Size);
        for (int copy = 0; copy < copies; copy++)
        {
            foreach (var suit in suits)
            {
                for (int rank = 1; rank <= 13; rank++)
                {
                    builder.Add(new Card(suit, rank));
                }
            }
        }

        return builder.MoveToImmutable();
    }

    /// <summary>Returns the ordered Spider deck shuffled with the seeded Fisher–Yates pass.</summary>
    public static ImmutableArray<Card> Shuffle(int seed, int suitCount)
    {
        var cards = BuildOrdered(suitCount).ToArray();
        Deck.ShuffleInPlace(cards, new DeterministicRandom(seed));
        return [.. cards];
    }

    /// <summary>
    /// Deals a shuffled 104-card deck into the initial Spider layout: 54 cards into
    /// ten columns (columns 0..3 get 6 cards, 4..9 get 5), each column's top card
    /// face-up; the remaining 50 cards become the stock (index 0 = next dealt).
    /// Dealt column-by-column from the front of <paramref name="shuffled"/>.
    /// </summary>
    public static (ImmutableArray<TableauPile> Tableau, ImmutableArray<Card> Stock) Deal(
        ImmutableArray<Card> shuffled)
    {
        if (shuffled.Length != Size)
        {
            throw new ArgumentException("A full 104-card Spider deck is required.", nameof(shuffled));
        }

        var tableau = ImmutableArray.CreateBuilder<TableauPile>(SpiderState.TableauCount);
        int next = 0;
        for (int c = 0; c < SpiderState.TableauCount; c++)
        {
            int n = ColumnDealSize(c);
            var column = ImmutableArray.CreateBuilder<Card>(n);
            for (int r = 0; r < n; r++)
            {
                column.Add(shuffled[next++]);
            }

            tableau.Add(new TableauPile(column.MoveToImmutable(), n - 1));
        }

        var stock = ImmutableArray.CreateBuilder<Card>(Size - next);
        for (int i = next; i < Size; i++)
        {
            stock.Add(shuffled[i]);
        }

        return (tableau.MoveToImmutable(), stock.MoveToImmutable());
    }
}
