using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace R4Everyone.Binary4Everyone.Tests;

public class R4CryptoTests
{
    [Theory]
    [InlineData("", "D41D8CD98F00B204E9800998ECF8427E")]
    [InlineData("a", "0CC175B9C0F1B6A831C399E269772661")]
    [InlineData("abc", "900150983CD24FB0D6963F7D28E17F72")]
    [InlineData("message digest", "F96B697D7CB7938D525A2F31AAF161D0")]
    [InlineData("abcdefghijklmnopqrstuvwxyz", "C3FCD3D76192E4007DFB496CCA67E13B")]
    [InlineData("12345678901234567890123456789012345678901234567890123456789012345678901234567890",
        "57EDF4A22BE3C955AC49DA2E2107B67A")]
    public void Md5_MatchesRfc1321TestSuite(string input, string expected)
    {
        Assert.Equal(expected, Convert.ToHexString(R4Md5.HashData(Encoding.ASCII.GetBytes(input))));
    }

    [Fact]
    public void Md5_MatchesSystemImplementationAcrossBlockBoundaries()
    {
        var random = new Random(1234);
        foreach (var length in new[] { 1, 55, 56, 57, 63, 64, 65, 119, 120, 127, 128, 129, 1000, 4096 })
        {
            var data = new byte[length];
            random.NextBytes(data);
            Assert.Equal(MD5.HashData(data), R4Md5.HashData(data));
        }
    }

    [Theory]
    // Test vectors from RFC 6229 are for longer keys; these are the classic Wikipedia vectors.
    [InlineData("Key", "Plaintext", "BBF316E8D940AF0AD3")]
    [InlineData("Wiki", "pedia", "1021BF0420")]
    [InlineData("Secret", "Attack at dawn", "45A01F645FC35B383552544B9BF5")]
    public void Rc4_MatchesKnownVectors(string key, string plaintext, string expected)
    {
        var encrypted = R4Rc4.Apply(Encoding.ASCII.GetBytes(key), Encoding.ASCII.GetBytes(plaintext));

        Assert.Equal(expected, Convert.ToHexString(encrypted));
        Assert.Equal(plaintext, Encoding.ASCII.GetString(R4Rc4.Apply(Encoding.ASCII.GetBytes(key), encrypted)));
    }
}
