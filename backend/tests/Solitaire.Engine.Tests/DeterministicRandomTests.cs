using Xunit;

namespace Solitaire.Engine.Tests;

public class DeterministicRandomTests
{
    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        var a = new DeterministicRandom(12345);
        var b = new DeterministicRandom(12345);

        for (int i = 0; i < 1000; i++)
        {
            Assert.Equal(a.NextUInt32(), b.NextUInt32());
        }
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentSequences()
    {
        var a = new DeterministicRandom(1);
        var b = new DeterministicRandom(2);

        bool anyDifferent = false;
        for (int i = 0; i < 10; i++)
        {
            if (a.NextUInt32() != b.NextUInt32())
            {
                anyDifferent = true;
            }
        }

        Assert.True(anyDifferent);
    }

    [Fact]
    public void NextInt_StaysWithinBounds()
    {
        var rng = new DeterministicRandom(999);
        for (int i = 0; i < 10_000; i++)
        {
            int value = rng.NextInt(52);
            Assert.InRange(value, 0, 51);
        }
    }

    [Fact]
    public void NextInt_WithBoundOne_IsAlwaysZero()
    {
        var rng = new DeterministicRandom(7);
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(0, rng.NextInt(1));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NextInt_WithNonPositiveBound_Throws(int bound)
    {
        var rng = new DeterministicRandom(0);
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(bound));
    }

    [Fact]
    public void KnownVector_Seed1_IsStable()
    {
        // Pins the exact mulberry32 output for seed 1 so the TypeScript port can
        // assert against the same numbers. If this changes, the deal contract broke.
        var rng = new DeterministicRandom(1);
        uint[] expected = [2693262067, 11749833, 2265367787, 4213581821, 4159151403];

        foreach (uint value in expected)
        {
            Assert.Equal(value, rng.NextUInt32());
        }
    }
}
