using System.Buffers.Binary;
using System.Text;

namespace R4Everyone.Utils;

public static class HexCheatTextCodec
{
    // -----------------------------
    // Public: Initial load formatting
    // -----------------------------

    /// <summary>
    /// Takes file-order uint[8] (little-endian words), displays as big-endian words (8 hex chars each),
    /// 2 words per line.
    /// </summary>
    public static string FormatInitialFromFileUIntsLittleEndian(uint[] wordsLe)
    {
        ArgumentNullException.ThrowIfNull(wordsLe);
        if (wordsLe.Length != 8) throw new ArgumentException("uint[] must be length 8.", nameof(wordsLe));

        // Convert each LE word -> display BE hex
        var wordsBeHex = wordsLe.Select(u =>
        {
            var be = BinaryPrimitives.ReverseEndianness(u);
            return be.ToString("X8");
        }).ToArray();

        return JoinWordsTwoPerLine(wordsBeHex);
    }

    /// <summary>
    /// Takes file-order List&lt;byte[4]&gt; (little-endian bytes per word),
    /// displays as big-endian words (8 hex chars each), 2 words per line.
    /// </summary>
    public static string FormatInitialFromFileWordsLittleEndian(List<byte[]> words4Le)
    {
        ArgumentNullException.ThrowIfNull(words4Le);
        if (words4Le.Any(w => w.Length != 4))
            throw new ArgumentException("Each byte[] must be exactly 4 bytes.", nameof(words4Le));

        var wordsBeHex = words4Le.Select(w =>
        {
            // file LE bytes -> display BE word value
            var leVal = BinaryPrimitives.ReadUInt32LittleEndian(w);
            var beVal = BinaryPrimitives.ReverseEndianness(leVal);
            return beVal.ToString("X8");
        }).ToArray();

        return JoinWordsTwoPerLine(wordsBeHex);
    }

    // -----------------------------
    // Public: Export/save (strip whitespace)
    // -----------------------------

    /// <summary>
    /// Parse user-edited text (any whitespace allowed). Interprets it as big-endian 32-bit words.
    /// Produces uint[8] in file-order (little-endian words when written via BinaryWriter.Write(uint)).
    /// </summary>
    public static uint[] ParseToFileUIntsLittleEndian(string text)
    {
        var hex = StripWhitespace(text);
        EnsureHexOnly(hex);

        // Need exactly 8 words => 8 * 8 hex chars = 64
        if (hex.Length != 8 * 8)
            throw new FormatException($"Expected exactly 64 hex characters (8 words), got {hex.Length}.");

        var words = new uint[8];
        for (var i = 0; i < 8; i++)
        {
            var be = ParseU32Hex(hex.AsSpan(i * 8, 8));
            // store as LE-word value so BinaryWriter.Write(uint) outputs little-endian bytes
            words[i] = BinaryPrimitives.ReverseEndianness(be);
        }

        return words;
    }

    /// <summary>
    /// Parse user-edited text (any whitespace allowed). Interprets it as big-endian 32-bit words.
    /// Produces List&lt;byte[4]&gt; in file-order (little-endian bytes per word).
    /// </summary>
    public static List<byte[]> ParseToFileWordsLittleEndian(string text)
    {
        var hex = StripWhitespace(text);
        EnsureHexOnly(hex);

        if (hex.Length % 8 != 0)
            throw new FormatException($"Hex length must be a multiple of 8 (32-bit words). Got {hex.Length}.");

        var wordCount = hex.Length / 8;
        var list = new List<byte[]>(wordCount);

        for (var i = 0; i < wordCount; i++)
        {
            var be = ParseU32Hex(hex.AsSpan(i * 8, 8));
            var leVal = BinaryPrimitives.ReverseEndianness(be);

            var bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, leVal);
            list.Add(bytes);
        }

        return list;
    }

    // -----------------------------
    // Helpers
    // -----------------------------

    private static string JoinWordsTwoPerLine(string[] wordHex)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < wordHex.Length; i++)
        {
            if (i > 0)
            {
                // newline after every 2 words
                sb.Append((i % 2 == 0) ? '\n' : ' ');
            }
            sb.Append(wordHex[i]);
        }
        return sb.ToString();
    }

    public static string StripWhitespace(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        foreach (var c in s.Where(c => !char.IsWhiteSpace(c)))
            sb.Append(c);
        return sb.ToString();
    }

    private static void EnsureHexOnly(string hex)
    {
        for (var i = 0; i < hex.Length; i++)
        {
            var c = hex[i];
            var ok =
                c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

            if (!ok)
                throw new FormatException($"Invalid character '{c}' at index {i}. Only hex and whitespace are allowed.");
        }
    }

    private static uint ParseU32Hex(ReadOnlySpan<char> eightHexChars)
    {
        // Manual parse (fast, no allocations)
        uint value = 0;
        for (var i = 0; i < 8; i++)
        {
            value = (value << 4) | Nibble(eightHexChars[i]);
        }
        return value;

        static uint Nibble(char c)
        {
            if (c is >= '0' and <= '9') return (uint)(c - '0');
            c = char.ToUpperInvariant(c);
            if (c is >= 'A' and <= 'F') return (uint)(10 + (c - 'A'));
            throw new FormatException($"Invalid hex character '{c}'.");
        }
    }
}