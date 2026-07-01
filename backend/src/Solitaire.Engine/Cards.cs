namespace Solitaire.Engine;

/// <summary>
/// The four French-deck suits. The integer values are part of the engine's
/// wire/deterministic contract and MUST match the TypeScript port:
/// Clubs = 0, Diamonds = 1, Hearts = 2, Spades = 3.
/// </summary>
public enum Suit
{
    Clubs = 0,
    Diamonds = 1,
    Hearts = 2,
    Spades = 3,
}

/// <summary>Card color, derived from <see cref="Suit"/>.</summary>
public enum CardColor
{
    Black = 0,
    Red = 1,
}

/// <summary>
/// A single playing card. <see cref="Rank"/> is 1..13 (Ace = 1, Jack = 11,
/// Queen = 12, King = 13).
/// </summary>
public readonly record struct Card(Suit Suit, int Rank)
{
    /// <summary>Ace rank constant.</summary>
    public const int Ace = 1;

    /// <summary>King rank constant.</summary>
    public const int King = 13;

    /// <summary>Red suits are Diamonds and Hearts; Clubs and Spades are black.</summary>
    public CardColor Color => Suit is Suit.Diamonds or Suit.Hearts ? CardColor.Red : CardColor.Black;

    /// <summary>
    /// Canonical 0..51 index used to build the ordered deck before shuffling.
    /// Suit-major: <c>index = (int)Suit * 13 + (Rank - 1)</c>. This ordering is
    /// part of the deterministic contract and must match the TypeScript port.
    /// </summary>
    public int OrdinalIndex => (int)Suit * 13 + (Rank - 1);

    /// <summary>Builds a card from its canonical 0..51 <see cref="OrdinalIndex"/>.</summary>
    public static Card FromOrdinalIndex(int index)
    {
        if (index is < 0 or > 51)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Card index must be 0..51.");
        }

        return new Card((Suit)(index / 13), (index % 13) + 1);
    }

    /// <summary>Short debug form, e.g. "AS", "TD", "KH", "2C".</summary>
    public override string ToString()
    {
        char rank = Rank switch
        {
            1 => 'A',
            10 => 'T',
            11 => 'J',
            12 => 'Q',
            13 => 'K',
            _ => (char)('0' + Rank),
        };
        char suit = Suit switch
        {
            Suit.Clubs => 'C',
            Suit.Diamonds => 'D',
            Suit.Hearts => 'H',
            Suit.Spades => 'S',
            _ => '?',
        };
        return $"{rank}{suit}";
    }
}
