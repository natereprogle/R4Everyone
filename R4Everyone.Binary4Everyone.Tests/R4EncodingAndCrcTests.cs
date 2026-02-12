using Xunit;

namespace R4Everyone.Binary4Everyone.Tests;

public class R4EncodingAndCrcTests
{
    [Theory]
    [InlineData(R4Encoding.GBK)]
    [InlineData(R4Encoding.BIG5)]
    [InlineData(R4Encoding.SJIS)]
    [InlineData(R4Encoding.UTF8)]
    public void GetEncoding_RoundTripsKnownEncodings(R4Encoding encoding)
    {
        var bytes = R4EncodingHelper.GetBytes(encoding);

        var parsed = R4EncodingHelper.GetEncoding(bytes);

        Assert.Equal(encoding, parsed);
    }

    [Fact]
    public void GetEncoding_ThrowsForUnknownByteSequence()
    {
        var bytes = "\0\0\0\0"u8.ToArray();

        Assert.Throws<ArgumentException>(() => R4EncodingHelper.GetEncoding(bytes));
    }

    [Fact]
    public void CalculateCrc32_ReturnsInvertedZeroForEmptyInput()
    {
        var result = Crc32Helper.CalculateCrc32([]);

        Assert.Equal(uint.MaxValue, result);
    }

    [Fact]
    public void ConvertCrc32ToString_ReturnsLowercaseHex()
    {
        const uint value = 0x0ABC0123u;

        var result = Crc32Helper.ConvertCrc32ToString(value);

        Assert.Equal("0abc0123", result);
    }
}
