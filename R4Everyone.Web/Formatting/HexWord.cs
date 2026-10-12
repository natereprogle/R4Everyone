using System.Globalization;

namespace R4Everyone.Web.Formatting;

/// <summary>
/// Helpers for the 4-byte words the editor shows as 8 hex characters.
/// </summary>
public static class HexWord
{
    public const int Length = 8;

    public static bool IsHexChar(char ch) => char.IsAsciiHexDigit(ch);

    public static bool IsValid(string? value) => value is { Length: Length } && value.All(IsHexChar);

    public static bool TryParse(string? value, out uint result)
    {
        result = 0;
        return IsValid(value) &&
               uint.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out result);
    }

    public static bool TryParseBytes(string? value, out byte[] bytes)
    {
        bytes = IsValid(value) ? Convert.FromHexString(value!) : [];
        return bytes.Length == 4;
    }

    public static string FromBytes(byte[] bytes) => bytes.Length == 4 ? Convert.ToHexString(bytes) : string.Empty;

    public static string FromUInt(uint value) => value.ToString("X8", CultureInfo.InvariantCulture);

    /// <summary>
    /// Keeps only hex characters (upper-cased), truncated to one word.
    /// </summary>
    public static string Sanitize(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var chars = input.Where(IsHexChar).Take(Length).Select(char.ToUpperInvariant).ToArray();
        return new string(chars);
    }

    /// <summary>
    /// Reverses the byte order of a full 8-character word. Partial words are returned unchanged.
    /// </summary>
    public static string SwapEndian(string value)
    {
        if (value.Length != Length)
        {
            return value;
        }

        return string.Concat(value.AsSpan(6, 2), value.AsSpan(4, 2), value.AsSpan(2, 2), value.AsSpan(0, 2));
    }
}
