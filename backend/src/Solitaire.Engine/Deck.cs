using System.Collections.Immutable;

namespace Solitaire.Engine;

/// <summary>
/// Deterministic deck construction: ordered deck, seeded shuffle, and the
/// Klondike deal. All three steps are fully specified so a TypeScript port can
/// reproduce identical deals from the same seed.
/// </summary>
public static class Deck
{
    /// <summary>Total cards in a standard deck.</summary>
    public const int Size = 52;

    /// <summary>Cards dealt to the tableau at the start of a game (1+2+...+7).</summary>
    public const int TableauDealCount = 28;

    /// <summary>
    /// The canonical unshuffled deck: card ordinal 0..51 in order
    /// (Clubs A..K, Diamonds A..K, Hearts A..K, Spades A..K). See
    /// <see cref="Card.OrdinalIndex"/>.
    /// </summary>
    public static ImmutableArray<Card> BuildOrdered()
    {
        var builder = ImmutableArray.CreateBuilder<Card>(Size);
        for (int i = 0; i < Size; i++)
        {
            builder.Add(Card.FromOrdinalIndex(i));
        }

        return builder.MoveToImmutable();
    }

    /// <summary>
    /// Returns the ordered deck shuffled with a seeded Fisher–Yates
    /// (Durstenfeld) pass.
    /// </summary>
    /// <remarks>
    /// Exact algorithm (reproduce identically in TypeScript):
    /// <code>
    /// deck = BuildOrdered()               // index 0 = Ace of Clubs, ...
    /// rng  = DeterministicRandom(seed)
    /// for i from 51 down to 1:            // inclusive
    ///     j = rng.NextInt(i + 1)          // 0 &lt;= j &lt;= i
    ///     swap deck[i], deck[j]
    /// </code>
    /// The loop runs from the last index down to 1 and swaps each element with a
    /// uniformly chosen earlier-or-equal index. <c>deck[0]</c> becomes the top of
    /// the deal (first card consumed).
    /// </remarks>
    public static ImmutableArray<Card> Shuffle(int seed)
    {
        var cards = BuildOrdered().ToArray();
        ShuffleInPlace(cards, new DeterministicRandom(seed));
        return [.. cards];
    }

    /// <summary>
    /// The shared seeded Fisher–Yates (Durstenfeld) shuffle used by every variant
    /// (Klondike's 52-card deck, Spider's 104-card deck, …). Reproduce identically
    /// in TypeScript: for <c>i</c> from <c>Count-1</c> down to 1, swap
    /// <c>items[i]</c> with <c>items[rng.NextInt(i + 1)]</c>.
    /// </summary>
    public static void ShuffleInPlace<T>(IList<T> items, DeterministicRandom rng)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(rng);

        for (int i = items.Count - 1; i >= 1; i--)
        {
            int j = rng.NextInt(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    /// <summary>
    /// Deals a shuffled deck into the initial Klondike layout.
    /// </summary>
    /// <remarks>
    /// Deal order (column-by-column, consuming <paramref name="shuffled"/> from
    /// the front): for column <c>c</c> in 0..6, take <c>c + 1</c> cards; the first
    /// taken sits at the bottom face-down and the last taken is face-up on top, so
    /// column <c>c</c> has <c>c</c> face-down cards and one face-up card. The
    /// remaining 24 cards become the stock with index 0 as the top (next drawn).
    /// </remarks>
    public static (ImmutableArray<TableauPile> Tableau, ImmutableArray<Card> Stock) Deal(
        ImmutableArray<Card> shuffled)
    {
        if (shuffled.Length != Size)
        {
            throw new ArgumentException("A full 52-card deck is required.", nameof(shuffled));
        }

        var tableau = ImmutableArray.CreateBuilder<TableauPile>(GameState.TableauCount);
        int next = 0;
        for (int c = 0; c < GameState.TableauCount; c++)
        {
            int cardsInColumn = c + 1;
            var column = ImmutableArray.CreateBuilder<Card>(cardsInColumn);
            for (int r = 0; r < cardsInColumn; r++)
            {
                column.Add(shuffled[next++]);
            }

            // All but the last dealt card are face-down.
            tableau.Add(new TableauPile(column.MoveToImmutable(), cardsInColumn - 1));
        }

        var stock = ImmutableArray.CreateBuilder<Card>(Size - next);
        for (int i = next; i < Size; i++)
        {
            stock.Add(shuffled[i]);
        }

        return (tableau.MoveToImmutable(), stock.MoveToImmutable());
    }
}
