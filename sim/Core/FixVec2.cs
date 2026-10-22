using System;

namespace ContingencyZero.Sim.Core;

/// <summary>2D position or direction on the ground plane, in fixed point. X runs east, Y runs south.</summary>
public readonly struct FixVec2 : IEquatable<FixVec2>
{
    public readonly Fix64 X;
    public readonly Fix64 Y;

    public FixVec2(Fix64 x, Fix64 y)
    {
        X = x;
        Y = y;
    }

    public static FixVec2 FromInts(int x, int y) => new(Fix64.FromInt(x), Fix64.FromInt(y));

    public static readonly FixVec2 Zero = new(Fix64.Zero, Fix64.Zero);

    public static FixVec2 operator +(FixVec2 a, FixVec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static FixVec2 operator -(FixVec2 a, FixVec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static FixVec2 operator *(FixVec2 a, Fix64 s) => new(a.X * s, a.Y * s);
    public static bool operator ==(FixVec2 a, FixVec2 b) => a.X == b.X && a.Y == b.Y;
    public static bool operator !=(FixVec2 a, FixVec2 b) => !(a == b);

    public Fix64 LengthSquared() => X * X + Y * Y;
    public Fix64 Length() => Fix64.Sqrt(LengthSquared());

    public static Fix64 DistanceSquared(FixVec2 a, FixVec2 b) => (a - b).LengthSquared();
    public static Fix64 Distance(FixVec2 a, FixVec2 b) => (a - b).Length();

    /// <summary>Moves from <paramref name="from"/> toward <paramref name="to"/> by at most <paramref name="step"/>.</summary>
    public static FixVec2 MoveTowards(FixVec2 from, FixVec2 to, Fix64 step)
    {
        FixVec2 delta = to - from;
        Fix64 distance = delta.Length();
        if (distance <= step || distance == Fix64.Zero) return to;
        return from + delta * (step / distance);
    }

    public bool Equals(FixVec2 other) => this == other;
    public override bool Equals(object? obj) => obj is FixVec2 v && this == v;
    public override int GetHashCode() => HashCode.Combine(X.Raw, Y.Raw);
    public override string ToString() => $"({X}, {Y})";
}
