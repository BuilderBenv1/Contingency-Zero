using ContingencyZero.Sim.Core;
using Xunit;

namespace ContingencyZero.Sim.Tests;

public class Fix64Tests
{
    [Fact]
    public void Arithmetic_is_exact_for_simple_values()
    {
        Fix64 a = Fix64.FromInt(6), b = Fix64.FromInt(4);
        Assert.Equal(Fix64.FromInt(10), a + b);
        Assert.Equal(Fix64.FromInt(2), a - b);
        Assert.Equal(Fix64.FromInt(24), a * b);
        Assert.Equal(Fix64.Parse("1.5"), a / b);
        Assert.Equal(Fix64.FromInt(-6), -a);
    }

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("1", 65536L)]
    [InlineData("-1", -65536L)]
    [InlineData("0.5", 32768L)]
    [InlineData("2.25", 147456L)]
    [InlineData("-0.75", -49152L)]
    [InlineData("1.2", 78643L)]
    [InlineData("+3", 196608L)]
    public void Parse_reads_decimal_text_without_floats(string text, long expectedRaw)
    {
        Assert.Equal(expectedRaw, Fix64.Parse(text).Raw);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1.2.3")]
    [InlineData("1e5")]
    public void Parse_rejects_bad_input(string text)
    {
        Assert.ThrowsAny<System.FormatException>(() => Fix64.Parse(text));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(4, 2)]
    [InlineData(144, 12)]
    [InlineData(1_000_000, 1000)]
    public void Sqrt_of_perfect_squares_is_exact(int value, int root)
    {
        Assert.Equal(Fix64.FromInt(root), Fix64.Sqrt(Fix64.FromInt(value)));
    }

    [Fact]
    public void Sqrt_of_two_is_accurate_to_the_last_bit()
    {
        // sqrt(2) * 65536 = 92681.9..., so the floor is 92681.
        Assert.Equal(92681L, Fix64.Sqrt(Fix64.FromInt(2)).Raw);
    }

    [Fact]
    public void Multiplying_large_values_does_not_overflow_the_intermediate()
    {
        Fix64 big = Fix64.FromInt(1_000_000);
        Assert.Equal(Fix64.FromInt(1_000_000) * 1_000_000, big * big);
    }

    [Fact]
    public void FromRatio_rounds_toward_zero()
    {
        Assert.Equal(21845L, Fix64.FromRatio(1, 3).Raw);
        Assert.Equal(-21845L, Fix64.FromRatio(-1, 3).Raw);
    }

    [Fact]
    public void MoveTowards_stops_exactly_on_the_target()
    {
        FixVec2 from = FixVec2.FromInts(0, 0), to = FixVec2.FromInts(3, 4);
        FixVec2 step1 = FixVec2.MoveTowards(from, to, Fix64.FromInt(4));
        Assert.True(step1.Length() <= Fix64.FromInt(4));
        Assert.Equal(to, FixVec2.MoveTowards(step1, to, Fix64.FromInt(4)));
    }
}
