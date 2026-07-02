import { isRed, Suit, type Card as CardValue } from '../../engine/cards';
import { ClubIcon, DiamondIcon, HeartIcon, SpadeIcon, type IconProps } from '../../icons/icons';

const RANK_LABELS: Readonly<Record<number, string>> = {
  1: 'A',
  11: 'J',
  12: 'Q',
  13: 'K',
};

function rankLabel(rank: number): string {
  return RANK_LABELS[rank] ?? String(rank);
}

function SuitGlyph({ suit, ...props }: IconProps & { suit: Suit }) {
  switch (suit) {
    case Suit.Clubs:
      return <ClubIcon {...props} />;
    case Suit.Diamonds:
      return <DiamondIcon {...props} />;
    case Suit.Hearts:
      return <HeartIcon {...props} />;
    default:
      return <SpadeIcon {...props} />;
  }
}

/** A single card face/back. Pure visual; interaction lives in the Pile. */
export function Card({ card, faceUp }: { card: CardValue; faceUp: boolean }) {
  const label = rankLabel(card.rank);
  return (
    <div className="card" data-faceup={faceUp} data-red={isRed(card.suit)}>
      <div className="card__inner">
        <div className="card__front">
          <span className="card__corner card__corner--tl">
            <span className="card__rank">{label}</span>
            <SuitGlyph suit={card.suit} className="card__pip" />
          </span>
          <span className="card__center">
            <SuitGlyph suit={card.suit} />
          </span>
          <span className="card__corner card__corner--br">
            <span className="card__rank">{label}</span>
            <SuitGlyph suit={card.suit} className="card__pip" />
          </span>
        </div>
        <div className="card__back">
          <span className="card__emblem" />
        </div>
      </div>
    </div>
  );
}
