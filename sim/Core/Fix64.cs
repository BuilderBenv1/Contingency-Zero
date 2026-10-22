using System;

namespace ContingencyZero.Sim.Core;

/// <summary>
/// Signed 48.16 fixed-point number. The only number type allowed for fractional values in the sim.
/// All arithmetic is integer arithmetic, so results are identical on every CPU and OS.
/// </summary>
public readonly struct Fix64 : IEquatable<Fix64>, IComparable<Fix64>, IComparable
{
    public const int FractionalBits = 16;
    public const long OneRaw = 1L << FractionalBits;

    public readonly long Raw;

    private Fix64(long raw) => Raw = raw;

    public static Fix64 FromRaw(long raw) => new(raw);
    public static Fix64 FromInt(int value) => new((long)value << FractionalBits);

    /// <summary>numerator / denominator, rounded toward zero.</summary>
    public static Fix64 FromRatio(long numerator, long denominator)
    {
        if (denominator == 0) throw new DivideByZeroException();
        return new((long)(((Int128)numerator << FractionalBits) / denominator));
    }

    public static readonly Fix64 Zero = new(0);
    public static readonly Fix64 One = new(OneRaw);
    public static readonly Fix64 Half = new(OneRaw / 2);
    public static readonly Fix64 MaxValue = new(long.MaxValue);
    public static readonly Fix64 MinValue = new(long.MinValue);

    public int ToIntFloor() => (int)(Raw >> FractionalBits);

    public static Fix64 operator +(Fix64 a, Fix64 b) => new(a.Raw + b.Raw);
    public static Fix64 operator -(Fix64 a, Fix64 b) => new(a.Raw - b.Raw);
    public static Fix64 operator -(Fix64 a) => new(-a.Raw);
    public static Fix64 operator *(Fix64 a, Fix64 b) => new((long)(((Int128)a.Raw * b.Raw) >> FractionalBits));
    public static Fix64 operator *(Fix64 a, int b) => new(a.Raw * b);

    public static Fix64 operator /(Fix64 a, Fix64 b)
    {
        if (b.Raw == 0) throw new DivideByZeroException();
        return new((long)(((Int128)a.Raw << FractionalBits) / b.Raw));
    }

    public static Fix64 operator /(Fix64 a, int b) => new(a.Raw / b);

    public static bool operator ==(Fix64 a, Fix64 b) => a.Raw == b.Raw;
    public static bool operator !=(Fix64 a, Fix64 b) => a.Raw != b.Raw;
    public static bool operator <(Fix64 a, Fix64 b) => a.Raw < b.Raw;
    public static bool operator >(Fix64 a, Fix64 b) => a.Raw > b.Raw;
    public static bool operator <=(Fix64 a, Fix64 b) => a.Raw <= b.Raw;
    public static bool operator >=(Fix64 a, Fix64 b) => a.Raw >= b.Raw;

    public static Fix64 Min(Fix64 a, Fix64 b) => a.Raw <= b.Raw ? a : b;
    public static Fix64 Max(Fix64 a, Fix64 b) => a.Raw >= b.Raw ? a : b;
    public static Fix64 Clamp(Fix64 v, Fix64 lo, Fix64 hi) => Max(lo, Min(hi, v));
    public static Fix64 Abs(Fix64 v) => v.Raw < 0 ? new(-v.Raw) : v;

    /// <summary>Integer square root on the raw value; exact to the last fractional bit (rounded down).</summary>
    public static Fix64 Sqrt(Fix64 v)
    {
        if (v.Raw < 0) throw new ArgumentOutOfRangeException(nameof(v), "Square root of a negative number");
        if (v.Raw == 0) return Zero;
        // sqrt(raw * 2^16) gives the result in raw units.
        UInt128 n = (UInt128)(ulong)v.Raw << FractionalBits;
        UInt128 x = n;
        UInt128 y = (x + 1) >> 1;
        while (y < x)
        {
            x = y;
            y = (x + n / x) >> 1;
        }
        return new((long)(ulong)x);
    }

    /// <summary>Parses a plain decimal string such as "12", "-0.75" or "3.125" without touching floating point.</summary>
    public static Fix64 Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new FormatException("Empty number");
        string s = text.Trim();
        bool negative = s[0] == '-';
        if (negative || s[0] == '+') s = s.Substring(1);

        int dot = s.IndexOf('.');
        string whole = dot < 0 ? s : s.Substring(0, dot);
        string frac = dot < 0 ? "" : s.Substring(dot + 1);
        if (whole.Length == 0 && frac.Length == 0) throw new FormatException($"Not a number: '{text}'");

        long wholeValue = 0;
        foreach (char c in whole)
        {
            if (c < '0' || c > '9') throw new FormatException($"Not a number: '{text}'");
            wholeValue = checked(wholeValue * 10 + (c - '0'));
        }

        long fracNumerator = 0, fracDenominator = 1;
        foreach (char c in frac)
        {
            if (c < '0' || c > '9') throw new FormatException($"Not a number: '{text}'");
            if (fracDenominator >= 100_000_000_000_000_000L) break; // 17 digits is more than 16 fractional bits can use
            fracNumerator = fracNumerator * 10 + (c - '0');
            fracDenominator *= 10;
        }

        long raw = checked((wholeValue << FractionalBits) + (long)(((Int128)fracNumerator << FractionalBits) / fracDenominator));
        return new(negative ? -raw : raw);
    }

    public bool Equals(Fix64 other) => Raw == other.Raw;
    public override bool Equals(object? obj) => obj is Fix64 f && f.Raw == Raw;
    public override int GetHashCode() => Raw.GetHashCode();
    public int CompareTo(Fix64 other) => Raw.CompareTo(other.Raw);
    public int CompareTo(object? obj) => obj is Fix64 f
        ? Raw.CompareTo(f.Raw)
        : throw new ArgumentException("Can only compare Fix64 with Fix64", nameof(obj));

    /// <summary>Debug display only. Never parse this back into sim state.</summary>
    public override string ToString()
    {
        long abs = Raw < 0 ? -Raw : Raw;
        long whole = abs >> FractionalBits;
        long frac = ((abs & (OneRaw - 1)) * 10000) >> FractionalBits;
        return $"{(Raw < 0 ? "-" : "")}{whole}.{frac:D4}";
    }
}
