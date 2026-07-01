namespace Solitaire.Engine;

/// <summary>
/// A tiny, fully-specified pseudo-random number generator (the "mulberry32"
/// algorithm) used so that a given integer seed always produces the same deal
/// on every platform and runtime.
/// </summary>
/// <remarks>
/// <para><b>Why a custom PRNG?</b> <see cref="System.Random"/> is explicitly not
/// guaranteed to be stable across .NET versions, and we need bit-for-bit
/// reproducibility with a future TypeScript port. mulberry32 is a well-known
/// 32-bit generator that is trivial to re-implement identically elsewhere.</para>
///
/// <para><b>Exact algorithm.</b> State is a single unsigned 32-bit integer.
/// All arithmetic below is performed modulo 2^32 (i.e. C# <c>unchecked</c>
/// <see cref="uint"/> math; in JavaScript use <c>Math.imul</c> for the
/// multiplications and <c>&gt;&gt;&gt; 0</c> to coerce back to uint32):</para>
/// <code>
/// nextUInt32():
///     state = (state + 0x6D2B79F5) mod 2^32
///     z = state
///     z = (z XOR (z >>> 15)) * (z OR 1)          // mod 2^32
///     z = z XOR (z + (z XOR (z >>> 7)) * (z OR 61))  // mod 2^32
///     return (z XOR (z >>> 14)) mod 2^32
/// </code>
/// where <c>>>></c> is a logical (zero-filling) right shift.
///
/// <para><b>Bounded integers.</b> <see cref="NextInt(int)"/> maps a 32-bit draw
/// to <c>[0, bound)</c> using the "multiply-high" (Lemire) reduction
/// <c>(uint)(((ulong)r * bound) >> 32)</c>. This is integer-only and therefore
/// identical across platforms. In JavaScript the same value is obtained with
/// <c>Math.floor((r * bound) / 2**32)</c>, which is exact for the small bounds
/// used here (bound &lt;= 52, so <c>r * bound &lt; 2^53</c>).</para>
/// </remarks>
public sealed class DeterministicRandom
{
    private uint _state;

    /// <summary>Creates a generator seeded with the given 32-bit seed.</summary>
    public DeterministicRandom(int seed) => _state = unchecked((uint)seed);

    /// <summary>Returns the next pseudo-random 32-bit value.</summary>
    public uint NextUInt32()
    {
        unchecked
        {
            _state += 0x6D2B79F5u;
            uint z = _state;
            z = (z ^ (z >> 15)) * (z | 1u);
            z ^= z + (z ^ (z >> 7)) * (z | 61u);
            return z ^ (z >> 14);
        }
    }

    /// <summary>
    /// Returns a value in <c>[0, bound)</c> using multiply-high reduction.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="bound"/> &lt;= 0.</exception>
    public int NextInt(int bound)
    {
        if (bound <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bound), bound, "bound must be positive.");
        }

        ulong product = (ulong)NextUInt32() * (ulong)(uint)bound;
        return (int)(product >> 32);
    }
}
