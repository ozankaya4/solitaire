// Bug #6 regression tests: foundation slots are not suit-fixed. An ace may be
// placed on ANY empty top slot (which then adopts that suit); an assigned slot
// only accepts its own suit.

import { describe, expect, it } from 'vitest';
import { KLONDIKE_UNLIMITED_REDEALS, type KlondikeState } from '../../engine/klondike';
import { Suit } from '../../engine/cards';
import { emptyPile } from '../../engine/tableau';
import { boardModel, UNASSIGNED_SLOTS } from '../boardModel';
import { moveBetween } from '../moves';

const OPTIONS = { drawCount: 1, maxRedeals: KLONDIKE_UNLIMITED_REDEALS };

function state(partial: Partial<KlondikeState>): KlondikeState {
  return {
    options: OPTIONS,
    stock: [],
    waste: [],
    foundations: [0, 0, 0, 0],
    tableau: [emptyPile, emptyPile, emptyPile, emptyPile, emptyPile, emptyPile, emptyPile],
    score: 0,
    redealsUsed: 0,
    ...partial,
  };
}

describe('foundation slot display mapping', () => {
  it('renders four unassigned slots as fslot placeholders', () => {
    const model = boardModel('klondike', state({}), UNASSIGNED_SLOTS);
    expect(model.foundations.map((p) => p.id)).toEqual([
      'fslot-0',
      'fslot-1',
      'fslot-2',
      'fslot-3',
    ]);
  });

  it('shows an assigned suit pile in its claimed slot', () => {
    const s = state({ foundations: [0, 0, 1, 0] }); // A♥ played
    const model = boardModel('klondike', s, [Suit.Hearts, null, null, null]);
    expect(model.foundations[0]!.id).toBe('foundation-2'); // hearts in slot 0
    expect(model.foundations[0]!.cards).toHaveLength(1);
    expect(model.foundations[1]!.id).toBe('fslot-1');
  });
});

describe('dropping onto foundation slots', () => {
  it('accepts any ace on any unassigned slot', () => {
    const s = state({ waste: [{ suit: Suit.Hearts, rank: 1 }] });
    const model = boardModel('klondike', s, UNASSIGNED_SLOTS);

    for (const slot of ['fslot-0', 'fslot-1', 'fslot-2', 'fslot-3']) {
      expect(moveBetween('klondike', s, model, 'waste', 0, slot)).toEqual({
        type: 'WasteToFoundation',
      });
    }
  });

  it('accepts an ace from the tableau on an empty slot', () => {
    const s = state({
      tableau: [
        { cards: [{ suit: Suit.Clubs, rank: 1 }], faceDownCount: 0 },
        emptyPile,
        emptyPile,
        emptyPile,
        emptyPile,
        emptyPile,
        emptyPile,
      ],
    });
    const model = boardModel('klondike', s, UNASSIGNED_SLOTS);
    expect(moveBetween('klondike', s, model, 'tableau-0', 0, 'fslot-2')).toEqual({
      type: 'TableauToFoundation',
      source: 0,
    });
  });

  it('rejects a non-ace on an unassigned slot', () => {
    const s = state({ waste: [{ suit: Suit.Hearts, rank: 5 }] });
    const model = boardModel('klondike', s, UNASSIGNED_SLOTS);
    expect(moveBetween('klondike', s, model, 'waste', 0, 'fslot-0')).toBeNull();
  });

  it('an assigned slot accepts only its own suit', () => {
    // Hearts foundation holds the ace and is assigned to slot 0.
    const s = state({
      foundations: [0, 0, 1, 0],
      waste: [{ suit: Suit.Hearts, rank: 2 }],
    });
    const slots = [Suit.Hearts, null, null, null];
    const model = boardModel('klondike', s, slots);

    // 2♥ onto the hearts slot → legal.
    expect(moveBetween('klondike', s, model, 'waste', 0, 'foundation-2')).toEqual({
      type: 'WasteToFoundation',
    });

    // 2♥ onto an unassigned slot → rejected (not an ace).
    expect(moveBetween('klondike', s, model, 'waste', 0, 'fslot-1')).toBeNull();

    // A♣ onto the hearts slot → rejected (wrong suit for that slot).
    const s2 = state({ foundations: [0, 0, 1, 0], waste: [{ suit: Suit.Clubs, rank: 1 }] });
    const model2 = boardModel('klondike', s2, slots);
    expect(moveBetween('klondike', s2, model2, 'waste', 0, 'foundation-2')).toBeNull();
    // …but it goes onto any free slot.
    expect(moveBetween('klondike', s2, model2, 'waste', 0, 'fslot-3')).toEqual({
      type: 'WasteToFoundation',
    });
  });
});
