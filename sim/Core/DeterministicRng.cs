namespace ContingencyZero.Sim.Core;

/// <summary>
/// PCG32 random number generator owned by the world. Seeded once per match; its state is part of the state hash.
/// The only source of randomness the sim may use.
/// </summary>
public sealed class DeterministicRng
{
    private const ulong Multiplier = 6364136223846793005UL;
    private const ulong Increment = 1442695040888963407UL;

    public ulong State { get; private set; }

    public DeterministicRng(ulong seed)
    {
        State = 0;
        NextUInt();
        State += seed;
        NextUInt();
    }

    public uint NextUInt()
    {
        ulong old = State;
        State = unchecked(old * Multiplier + Increment);
        uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
        int rot = (int)(old >> 59);
        return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
    }

    /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
    public int Range(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        uint span = (uint)(maxExclusive - minInclusive);
        return minInclusive + (int)(((ulong)NextUInt() * span) >> 32);
    }

    /// <summary>Uniform value in [0, 1).</summary>
    public Fix64 NextFix() => Fix64.FromRaw(NextUInt() >> 16);

    /// <summary>True with the given probability (0 to 1).</summary>
    public bool Chance(Fix64 probability) => NextFix() < probability;
}
