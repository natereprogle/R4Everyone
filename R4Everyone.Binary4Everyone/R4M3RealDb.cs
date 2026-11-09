using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace R4Everyone.Binary4Everyone;

/// <summary>
/// How an M3 DS Real database is laid out. These match r4cce's "M3 Real" settings.
/// </summary>
public enum R4M3RealMode
{
    /// <summary>Writes the game title, notes, and folder separators. One-hot folders are not marked.</summary>
    Extended,

    /// <summary>DHT-compatible minimal output: no title, notes, or separators. One-hot folders use <c>{…:}</c>.</summary>
    Dht
}

/// <summary>
/// Where notes are written relative to the item they describe.
/// </summary>
public enum R4M3RealNotePosition
{
    BeforeName,
    AfterName
}

public sealed record R4M3RealOptions
{
    public R4M3RealMode Mode { get; init; } = R4M3RealMode.Extended;
    public R4M3RealNotePosition NotePosition { get; init; } = R4M3RealNotePosition.BeforeName;

    /// <summary>
    /// Rewrites characters that would break the <c>[…]</c>, <c>&lt;…:&gt;</c>, and <c>{…:}</c> syntax, including
    /// double-byte characters whose trail byte is <c>]</c>, <c>&gt;</c>, or <c>}</c> (r4cce's "Character Fix").
    /// </summary>
    public bool CharacterFix { get; init; } = true;

    /// <summary>
    /// The text encoding. A Japanese M3 DS Real presumably expects Shift-JIS.
    /// </summary>
    public R4Encoding Encoding { get; init; } = R4Encoding.SJIS;
}

/// <summary>
/// Reads and writes the M3 DS Real <c>.db</c> format. The writer matches r4cce 0.86'; r4cce has no reader.
/// </summary>
/// <remarks>
/// The format is a binary game index followed by one CRLF text section per game. It does not store ON/OFF state,
/// master codes, the database title, or game enable flags. Extended mode also loses the one-hot flag, and DHT mode
/// loses notes. Cheats in nested folders are written into their top-level folder.
/// </remarks>
public static partial class R4M3RealDb
{
    private const int IndexEntryLength = 23;
    private const string NewLine = "\r\n";
    private static readonly byte[] Separator = "<----------:>"u8.ToArray();

    static R4M3RealDb()
    {
        System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static byte[] Export(R4Database db, R4M3RealOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        options ??= new R4M3RealOptions();
        db.MaterializeAllGames();

        if (db.Games.Count > ushort.MaxValue)
        {
            throw new InvalidOperationException(
                $"M3 DS Real files can hold at most {ushort.MaxValue} games, but this file has {db.Games.Count}.");
        }

        var encoding = GetEncoding(options.Encoding);
        var writer = new SectionWriter(encoding, options);

        using var output = new MemoryStream();
        Span<byte> count = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(count, (ushort)db.Games.Count);
        output.Write(count);

        foreach (var game in db.Games)
        {
            var crc = R4EvolutionCheats.ParseChecksum(game);
            output.Write(Encoding.ASCII.GetBytes(((game.GameId ?? string.Empty) + "????")[..4]));
            output.Write("DHT"u8);
            output.Write(Encoding.ASCII.GetBytes(crc.ToString("X8", CultureInfo.InvariantCulture)));
            output.Write(new byte[8]); // offset and length, patched below
        }

        output.Write(Encoding.ASCII.GetBytes(NewLine));

        for (var n = 0; n < db.Games.Count; n++)
        {
            var start = (uint)output.Length;
            var section = writer.WriteGame(db.Games[n]);
            output.Write(section);

            var entry = output.GetBuffer().AsSpan(2 + IndexEntryLength * n + 15, 8);
            BinaryPrimitives.WriteUInt32LittleEndian(entry, start);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[4..], (uint)section.Length);
        }

        return output.ToArray();
    }

    /// <summary>
    /// Returns the .NET encoding used for an <see cref="R4Encoding"/>. Unrepresentable characters become <c>?</c>.
    /// </summary>
    public static Encoding GetEncoding(R4Encoding encoding) => encoding switch
    {
        R4Encoding.SJIS => System.Text.Encoding.GetEncoding(932),
        R4Encoding.GBK => System.Text.Encoding.GetEncoding(936),
        R4Encoding.BIG5 => System.Text.Encoding.GetEncoding(950),
        R4Encoding.UTF8 => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, null)
    };

    /// <summary>
    /// r4cce's "Character Fix" for code names (<paramref name="isCodeName"/>) or for folder names and notes.
    /// </summary>
    /// <remarks>
    /// r4cce detects a double-byte character by checking whether the byte before a <c>]</c>, <c>&gt;</c>, or
    /// <c>}</c> is a Shift-JIS lead byte. That misfires when the previous byte is itself a trail byte, so this checks
    /// each character's own encoded bytes instead. The result is the same for well-formed Shift-JIS, and it is also
    /// correct for GBK, Big5, and UTF-8.
    /// </remarks>
    internal static string FixName(string text, bool isCodeName, Encoding encoding)
    {
        string bad;
        char replacement;
        if (isCodeName)
        {
            text = text.Replace('ゾ', 'ぞ').Replace('[', '<');
            bad = "]";
            replacement = '>';
        }
        else
        {
            text = text.Replace('マ', 'ま').Replace('<', '[').Replace('{', '[');
            bad = ">}";
            replacement = ']';
        }

        var result = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.IsAscii)
            {
                var c = (char)rune.Value;
                result.Append(bad.Contains(c) ? replacement : c);
                continue;
            }

            var bytes = encoding.GetBytes(rune.ToString());
            if (bytes.Skip(1).Any(b => bad.Contains((char)b)))
            {
                // A dame-moji: its trail byte would end the name early, so blank out the whole character.
                result.Append(' ', bytes.Length);
            }
            else
            {
                result.Append(rune.ToString());
            }
        }

        return result.ToString();
    }

    private sealed class SectionWriter(Encoding encoding, R4M3RealOptions options)
    {
        private readonly bool _dht = options.Mode == R4M3RealMode.Dht;
        private readonly bool _noteFirst = options.NotePosition == R4M3RealNotePosition.BeforeName;
        private MemoryStream _output = new();

        public byte[] WriteGame(R4Game game)
        {
            _output = new MemoryStream();
            var crc = R4EvolutionCheats.ParseChecksum(game);
            Line($";Game ID: {game.GameId} {crc:X8}");
            if (!_dht)
            {
                Line($";{game.GameTitle}{NewLine}{NewLine}");
            }

            var items = game.Items;
            for (var i = 0; i < items.Count; i++)
            {
                switch (items[i])
                {
                    case R4Folder folder:
                        WriteFolder(folder, isAfterCode: i > 0 && items[i - 1] is not R4Folder,
                            hasNextSibling: i + 1 < items.Count);
                        break;
                    case R4Cheat cheat:
                        WriteCode(cheat);
                        break;
                }
            }

            _output.WriteByte(0);
            return _output.ToArray();
        }

        private void WriteFolder(R4Folder folder, bool isAfterCode, bool hasNextSibling)
        {
            var cheats = EnumerateCheats(folder.Items).ToList();
            var name = FixFolder(folder.Title);
            if (!_dht)
            {
                if (isAfterCode)
                {
                    RawLine(Separator);
                }

                Note(folder.Description, before: true);
                Line($"<{name}:> = {cheats.Count}");
                Note(folder.Description, before: false);
            }
            else if (folder.OneHot)
            {
                Line($"{{{name}:}} = {cheats.Count}");
            }
            else
            {
                Line($"<{name}:> = {cheats.Count}");
            }

            foreach (var cheat in cheats)
            {
                WriteCode(cheat);
            }

            if (!_dht && hasNextSibling)
            {
                RawLine(Separator);
                RawLine([]);
            }
        }

        private void WriteCode(R4Cheat cheat)
        {
            Note(cheat.Description, before: true);
            Line($"[{(options.CharacterFix ? FixName(cheat.Title, isCodeName: true, encoding) : cheat.Title)}]");

            // An unpaired final word is dropped, as r4cce does.
            for (var i = 0; i + 1 < cheat.Code.Count; i += 2)
            {
                Line($"{ReadWord(cheat, i):X8} {ReadWord(cheat, i + 1):X8}");
            }

            Note(cheat.Description, before: false);
            Line(string.Empty);
        }

        private void Note(string? note, bool before)
        {
            if (!_dht && !string.IsNullOrEmpty(note) && _noteFirst == before)
            {
                Line($"<{FixFolder(note)}:>");
            }
        }

        private string FixFolder(string text) =>
            options.CharacterFix ? FixName(text, isCodeName: false, encoding) : text;

        private void Line(string text) => _output.Write(encoding.GetBytes(text + NewLine));

        private void RawLine(ReadOnlySpan<byte> bytes)
        {
            _output.Write(bytes);
            _output.Write("\r\n"u8);
        }

        private static uint ReadWord(R4Cheat cheat, int index)
        {
            var bytes = cheat.Code[index];
            return bytes.Length == 4
                ? BinaryPrimitives.ReadUInt32LittleEndian(bytes)
                : throw new InvalidOperationException($"Cheat '{cheat.Title}' has a code block that is not 4 bytes.");
        }
    }

    private static IEnumerable<R4Cheat> EnumerateCheats(IEnumerable<IR4Item> items)
    {
        foreach (var item in items)
        {
            switch (item)
            {
                case R4Cheat cheat:
                    yield return cheat;
                    break;
                case R4Folder folder:
                    foreach (var nested in EnumerateCheats(folder.Items))
                    {
                        yield return nested;
                    }

                    break;
            }
        }
    }
}
