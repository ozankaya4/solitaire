import type { ReactNode } from 'react';
import type { VariantId } from '../app/types';
import { ClubIcon, DiamondIcon, HeartIcon, SpadeIcon } from '../icons/icons';

/** A suit glyph per variant — decorative flavor for menus. */
export function variantIcon(id: VariantId, size = 18): ReactNode {
  switch (id) {
    case 'klondike':
      return <SpadeIcon size={size} />;
    case 'spider':
      return <ClubIcon size={size} />;
    case 'freecell':
      return <DiamondIcon size={size} />;
    case 'pyramid':
      return <HeartIcon size={size} />;
    case 'tripeaks':
      return <SpadeIcon size={size} />;
    default:
      return null;
  }
}
