using System.Text;

namespace R4Everyone.Web.Formatting;

/// <summary>
/// Text produced by <see cref="CheatCodeText.Format"/>, with the caret mapped into the new text.
/// </summary>
public readonly record struct FormattedCheatText(string Text, int SelectionStart, int SelectionEnd);

/// <summary>
/// Converts between the free-form cheat code textarea and code words. Each row of the textarea holds two
/// 8-character hex words separated by a space.
/// </summary>
public static class CheatCodeText
{
    /// <summary>
    /// Re-formats user input into rows of two words, dropping non-hex characters and keeping the caret on the
    /// same hex digit it was next to before formatting.
    /// </summary>
    public static FormattedCheatText Format(string input, int selectionStart, int selectionEnd)
    {
        input = NormalizeLineEndings(input, ref selectionStart, ref selectionEnd);
        if (input.Length == 0)
        {
            return new FormattedCheatText(string.Empty, 0, 0);
        }

        var lines = input.Split('\n');
        var builder = new StringBuilder();
        var lineStart = 0;
        int? newStart = null;
        int? newEnd = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineEnd = lineStart + line.Length;
            var formattedLine = FormatLine(line);

            if (newStart == null && selectionStart >= lineStart && selectionStart <= lineEnd)
            {
                newStart = builder.Length + MapCaret(line, selectionStart - lineStart, formattedLine);
            }

            if (newEnd == null && selectionEnd >= lineStart && selectionEnd <= lineEnd)
            {
                newEnd = builder.Length + MapCaret(line, selectionEnd - lineStart, formattedLine);
            }

            builder.Append(formattedLine);
            if (i < lines.Length - 1)
            {
                builder.Append('\n');
            }

            lineStart = lineEnd + 1;
        }

        return new FormattedCheatText(builder.ToString(), newStart ?? builder.Length, newEnd ?? builder.Length);
    }

    /// <summary>
    /// Splits text into words, two per row. A row with an odd number of words is padded with an empty word, so
    /// the result always has an even count. Words may be shorter than 8 characters while the user is typing.
    /// </summary>
    public static List<string> ParseWords(string text)
    {
        var words = new List<string>();
        foreach (var line in SplitLines(text))
        {
            var chunks = ChunkHex(line);
            for (var i = 0; i < chunks.Count; i += 2)
            {
                words.Add(chunks[i]);
                words.Add(i + 1 < chunks.Count ? chunks[i + 1] : string.Empty);
            }
        }

        return words;
    }

    /// <summary>
    /// Joins words back into display text, two per row.
    /// </summary>
    public static string FromWords(IEnumerable<string> words)
    {
        var list = words.ToList();
        var rows = new List<string>();
        for (var i = 0; i < list.Count; i += 2)
        {
            var left = list[i];
            var right = i + 1 < list.Count ? list[i + 1] : string.Empty;
            if (left.Length == 0 && right.Length == 0)
            {
                continue;
            }

            rows.Add(right.Length == 0 ? left : $"{left} {right}");
        }

        return string.Join('\n', rows);
    }

    /// <summary>
    /// Returns one message per non-empty row that is not exactly two full words.
    /// </summary>
    public static List<string> GetRowErrors(string text)
    {
        var errors = new List<string>();
        var lines = SplitLines(text);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length != 2 || !HexWord.IsValid(words[0]) || !HexWord.IsValid(words[1]))
            {
                errors.Add($"Row {i + 1} must contain 2 blocks of 8 hex characters.");
            }
        }

        return errors;
    }

    private static string[] SplitLines(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    private static string NormalizeLineEndings(string input, ref int selectionStart, ref int selectionEnd)
    {
        if (!input.Contains('\r'))
        {
            return input;
        }

        var builder = new StringBuilder(input.Length);
        var start = selectionStart;
        var end = selectionEnd;
        for (var i = 0; i < input.Length; i++)
        {
            if (input[i] == '\r')
            {
                if (i < selectionStart) start--;
                if (i < selectionEnd) end--;
                continue;
            }

            builder.Append(input[i]);
        }

        selectionStart = Math.Max(0, start);
        selectionEnd = Math.Max(0, end);
        return builder.ToString();
    }

    private static string FormatLine(string line) => FromWords(PairUp(ChunkHex(line)));

    private static IEnumerable<string> PairUp(List<string> chunks)
    {
        for (var i = 0; i < chunks.Count; i += 2)
        {
            yield return chunks[i];
            yield return i + 1 < chunks.Count ? chunks[i + 1] : string.Empty;
        }
    }

    private static List<string> ChunkHex(string line)
    {
        var hex = line.Where(HexWord.IsHexChar).Select(char.ToUpperInvariant).ToArray();
        var chunks = new List<string>();
        for (var i = 0; i < hex.Length; i += HexWord.Length)
        {
            chunks.Add(new string(hex, i, Math.Min(HexWord.Length, hex.Length - i)));
        }

        return chunks;
    }

    /// <summary>
    /// Maps a caret offset in the raw line to the offset just after the same hex digit in the formatted line.
    /// </summary>
    private static int MapCaret(string rawLine, int caret, string formattedLine)
    {
        var hexBefore = rawLine.Take(Math.Clamp(caret, 0, rawLine.Length)).Count(HexWord.IsHexChar);
        if (hexBefore == 0)
        {
            return 0;
        }

        var seen = 0;
        for (var i = 0; i < formattedLine.Length; i++)
        {
            if (HexWord.IsHexChar(formattedLine[i]) && ++seen == hexBefore)
            {
                return i + 1;
            }
        }

        return formattedLine.Length;
    }
}
