// Deterministic PRNG — an exact port of DeterministicRandom.cs (mulberry32).
// All arithmetic is performed modulo 2^32: `Math.imul` gives the wrapping 32-bit
// multiply and `>>> 0` coerces back to an unsigned 32-bit value, mirroring C#
// `unchecked` uint math so the same seed yields the same sequence on both sides.

export class DeterministicRandom {
  private state: number;

  constructor(seed: number) {
    this.state = seed >>> 0;
  }

  /** Returns the next pseudo-random 32-bit value. */
  nextUint32(): number {
    this.state = (this.state + 0x6d2b79f5) >>> 0;
    let z = this.state;
    z = Math.imul(z ^ (z >>> 15), z | 1) >>> 0;
    z = (z ^ (z + Math.imul(z ^ (z >>> 7), z | 61))) >>> 0;
    return (z ^ (z >>> 14)) >>> 0;
  }

  /**
   * Returns a value in `[0, bound)` using the same multiply-high (Lemire)
   * reduction as the C# engine: `(r * bound) >> 32`. Exact in JS doubles for the
   * small bounds used here (bound <= 104, so `r * bound < 2^53`).
   */
  nextInt(bound: number): number {
    if (bound <= 0) {
      throw new RangeError('bound must be positive.');
    }
    return Math.floor((this.nextUint32() * bound) / 4294967296);
  }
}
