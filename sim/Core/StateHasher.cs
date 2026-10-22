namespace ContingencyZero.Sim.Core;

/// <summary>FNV-1a 64-bit hash, fed in a fixed order. Used to detect desyncs and to pin golden replays.</summary>
public struct StateHasher
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    private ulong _hash;
    private bool _started;

    public ulong Value => _started ? _hash : OffsetBasis;

    public void Add(ulong value)
    {
        if (!_started)
        {
            _hash = OffsetBasis;
            _started = true;
        }
        for (int i = 0; i < 8; i++)
        {
            _hash ^= (byte)(value >> (i * 8));
            _hash = unchecked(_hash * Prime);
        }
    }

    public void Add(long value) => Add(unchecked((ulong)value));
    public void Add(int value) => Add(unchecked((ulong)(long)value));
    public void Add(bool value) => Add(value ? 1UL : 0UL);
    public void Add(Fix64 value) => Add(value.Raw);

    public void Add(FixVec2 value)
    {
        Add(value.X);
        Add(value.Y);
    }
}
