using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace R4Everyone.Binary4Everyone;

/// <summary>
/// Settings for reading an M3 DS Real file. The file does not record them, so a null value is detected.
/// </summary>
public sealed record R4M3RealReadOptions
{
    /// <summary>The text encoding, or null to detect UTF-8 and otherwise assume Shift-JIS.</summary>
    public R4Encoding? Encoding { get; init; }

    /// <summary>Whether notes precede or follow their item, or null to detect it from the layout.</summary>
    public R4M3RealNotePosition? NotePosition { get; init; }
}

/// <summary>
/// What <see cref="R4M3RealDb.Inspect"/> found in an M3 DS Real file.
/// </summary>
/// <param name="Encoding">UTF-8 if every section is valid UTF-8, otherwise Shift-JIS. GBK and Big5 cannot be told apart
/// from Shift-JIS, so the caller may need to ask.</param>
/// <param name="NotePosition">Detected from where note lines sit; BeforeName if the file has no notes.</param>
/// <param name="Mode">DHT if no section has a title comment.</param>
/// <param name="CharacterFix">True if every name and note is unchanged by r4cce's "Character Fix", so writing with it
/// on keeps the text as read.</param>
public sealed record R4M3RealFileInfo(
    int GameCount,
    R4Encoding Encoding,
    R4M3RealNotePosition NotePosition,
    R4M3RealMode Mode,
    bool CharacterFix)
{
    /// <summary>Options that write the file back the way it was read.</summary>
    public R4M3RealOptions ToWriteOptions() =>
        new() { Encoding = Encoding, NotePosition = NotePosition, Mode = Mode, CharacterFix = CharacterFix };
}

public static partial class R4M3RealDb
{
    private static readonly byte[] IndexTag = "DHT"u8.ToArray();

    [GeneratedRegex(@"^;Game ID: (?<id>.*) (?<crc>[0-9A-Fa-f]{8})$")]
    private static partial Regex GameIdLine();

    [GeneratedRegex(@"^(?<open>[<{])(?<name>.*):[>}] = (?<count>\d+)$")]
    private static partial Regex FolderLine();

    [GeneratedRegex(@"^<(?<text>.*):>$")]
    private static partial Regex NoteLine();

    [GeneratedRegex(@"^\[(?<name>.*)\]$")]
    private static partial Regex CodeLine();

    [GeneratedRegex(@"^(?<address>[0-9A-Fa-f]{8}) (?<value>[0-9A-Fa-f]{8})$")]
    private static partial Regex RowLine();

    /// <summary>
    /// Returns true if the data has a valid M3 DS Real index. The format has no magic bytes, so this checks that every
    /// index entry is tagged <c>DHT</c>, has a hex CRC, and points at a section inside the file.
    /// </summary>
    public static bool TryDetect(ReadOnlySpan<byte> data) => TryReadIndex(data, out _);

    /// <summary>
    /// Detects the settings a file was most likely written with, without changing anything.
    /// </summary>
    /// <param name="options">Settings the caller already knows; they replace detection, and Character Fix is then
    /// detected using them.</param>
    /// <exception cref="InvalidDataException">The data is not an M3 DS Real file.</exception>
    public static R4M3RealFileInfo Inspect(ReadOnlySpan<byte> data, R4M3RealReadOptions? options = null)
    {
        var index = ReadIndex(data);
        var sections = new List<byte[]>(index.Count);
        foreach (var entry in index)
        {
            sections.Add(GetSectionBytes(data, entry));
        }

        var encoding = options?.Encoding ?? (sections.All(IsValidUtf8) ? R4Encoding.UTF8 : R4Encoding.SJIS);
        var textEncoding = GetEncoding(encoding);
        var lines = sections.Select(section => SplitLines(textEncoding.GetString(section))).ToList();
        var notePosition = options?.NotePosition ?? DetectNotePosition(lines);

        var characterFix = true;
        for (var n = 0; n < index.Count && characterFix; n++)
        {
            var game = ReadGame(lines[n], index[n], notePosition == R4M3RealNotePosition.BeforeName, $"Game {n + 1}");
            characterFix = IsCharacterFixed(game.Items, textEncoding);
        }

        return new R4M3RealFileInfo(index.Count, encoding, notePosition, DetectMode(lines), characterFix);
    }

    private static bool IsCharacterFixed(IEnumerable<IR4Item> items, Encoding encoding) => items.All(item => item switch
    {
        R4Cheat cheat => FixName(cheat.Title, isCodeName: true, encoding) == cheat.Title &&
                         FixName(cheat.Description, isCodeName: false, encoding) == cheat.Description,
        R4Folder folder => FixName(folder.Title, isCodeName: false, encoding) == folder.Title &&
                           FixName(folder.Description, isCodeName: false, encoding) == folder.Description &&
                           IsCharacterFixed(folder.Items, encoding),
        _ => true
    });

    /// <summary>
    /// Reads an M3 DS Real file into a new, fully materialized database.
    /// </summary>
    /// <remarks>
    /// The format does not store ON/OFF states, master codes, the database title, or game enable flags, so cheats load
    /// disabled and the rest use defaults. Text changed by r4cce's "Character Fix" cannot be restored. Games without a
    /// title comment (DHT mode) use their game ID as the title.
    /// </remarks>
    /// <exception cref="InvalidDataException">The data is not a valid M3 DS Real file.</exception>
    public static R4Database Load(ReadOnlySpan<byte> data, R4M3RealReadOptions? options = null)
    {
        var index = ReadIndex(data);
        var info = options is { Encoding: not null, NotePosition: not null } ? null : Inspect(data);
        var encoding = GetEncoding(options?.Encoding ?? info!.Encoding);
        var noteFirst = (options?.NotePosition ?? info!.NotePosition) == R4M3RealNotePosition.BeforeName;

        var db = new R4Database { FileEncoding = R4Encoding.UTF8, Enabled = true };
        for (var n = 0; n < index.Count; n++)
        {
            var entry = index[n];
            var lines = SplitLines(encoding.GetString(GetSectionBytes(data, entry)));
            var game = ReadGame(lines, entry, noteFirst, $"Game {n + 1} ({entry.GameId})");
            db.Games.Add(game);
            db.MarkGameDirty(game);
        }

        return db;
    }

    private static R4Game ReadGame(IReadOnlyList<string> lines, IndexEntry entry, bool noteFirst, string context)
    {
        var game = new R4Game(entry.GameId)
        {
            GameChecksum = entry.Checksum,
            GameTitle = entry.GameId,
            GameEnabled = true
        };

        var position = 0;
        if (position < lines.Count && GameIdLine().Match(lines[position]) is { Success: true } idLine)
        {
            // The comment has the unpadded ID; the index pads short IDs with '?'.
            game.GameId = idLine.Groups["id"].Value;
            position++;
        }

        if (position < lines.Count && lines[position].StartsWith(';'))
        {
            game.GameTitle = lines[position][1..];
            position++;
        }
        else
        {
            game.GameTitle = game.GameId;
        }

        R4Folder? folder = null;
        var folderRemaining = 0;
        R4Cheat? code = null;
        IR4Item? lastItem = null;
        string? pendingNote = null;

        for (; position < lines.Count; position++)
        {
            var line = lines[position];
            if (line.Length == 0 || line == SeparatorText)
            {
                code = null;
                continue;
            }

            if (RowLine().Match(line) is { Success: true } row)
            {
                if (code == null)
                {
                    throw new InvalidDataException($"{context} has a code row outside a code on line {position + 1}.");
                }

                code.Code.Add(BitConverter.GetBytes(ParseHex(row.Groups["address"].Value)));
                code.Code.Add(BitConverter.GetBytes(ParseHex(row.Groups["value"].Value)));
                continue;
            }

            if (FolderLine().Match(line) is { Success: true } folderLine)
            {
                folder = new R4Folder
                {
                    Title = folderLine.Groups["name"].Value,
                    Description = TakeNote(ref pendingNote, noteFirst),
                    OneHot = folderLine.Groups["open"].Value == "{"
                };
                folderRemaining = int.Parse(folderLine.Groups["count"].Value, CultureInfo.InvariantCulture);
                game.Items.Add(folder);
                code = null;
                lastItem = folder;
                if (folderRemaining == 0)
                {
                    folder = null;
                }

                continue;
            }

            if (CodeLine().Match(line) is { Success: true } codeLine)
            {
                code = new R4Cheat
                {
                    Title = codeLine.Groups["name"].Value,
                    Description = TakeNote(ref pendingNote, noteFirst),
                    Enabled = false,
                    Code = []
                };
                if (folder != null)
                {
                    folder.Items.Add(code);
                    if (--folderRemaining == 0)
                    {
                        folder = null;
                    }
                }
                else
                {
                    game.Items.Add(code);
                }

                lastItem = code;
                continue;
            }

            if (NoteLine().Match(line) is { Success: true } note)
            {
                var text = note.Groups["text"].Value;
                if (noteFirst)
                {
                    pendingNote = text;
                }
                else
                {
                    switch (lastItem)
                    {
                        case R4Cheat cheat:
                            cheat.Description = text;
                            break;
                        case R4Folder noted:
                            noted.Description = text;
                            break;
                    }
                }

                continue;
            }

            if (line.StartsWith(';'))
            {
                continue;
            }

            throw new InvalidDataException($"{context} has an unrecognized line {position + 1}: '{line}'.");
        }

        return game;
    }

    private static string TakeNote(ref string? pendingNote, bool noteFirst)
    {
        var note = noteFirst ? pendingNote ?? string.Empty : string.Empty;
        pendingNote = null;
        return note;
    }

    // With notes after their item, a code's note directly follows its last row or its name; with notes first, that
    // never happens because every code ends with a blank line. A note after a folder header is ambiguous (the folder's
    // note-after, or its first code's note-before), so it is not evidence either way.
    private static R4M3RealNotePosition DetectNotePosition(IEnumerable<IReadOnlyList<string>> sections)
    {
        foreach (var lines in sections)
        {
            for (var i = 1; i < lines.Count; i++)
            {
                if (lines[i] == SeparatorText || !NoteLine().IsMatch(lines[i]))
                {
                    continue;
                }

                var previous = lines[i - 1];
                if (RowLine().IsMatch(previous) || CodeLine().IsMatch(previous))
                {
                    return R4M3RealNotePosition.AfterName;
                }
            }
        }

        return R4M3RealNotePosition.BeforeName;
    }

    private static R4M3RealMode DetectMode(IEnumerable<IReadOnlyList<string>> sections) =>
        sections.Any(lines => lines.Count > 1 && lines[1].StartsWith(';')) ? R4M3RealMode.Extended : R4M3RealMode.Dht;

    private static string SeparatorText => Encoding.ASCII.GetString(Separator);

    private static uint ParseHex(string hex) => uint.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static IReadOnlyList<string> SplitLines(string section) =>
        section.Split("\r\n").Select(line => line.TrimEnd('\r', '\n')).ToList();

    private static bool IsValidUtf8(byte[] bytes)
    {
        try
        {
            _ = new UTF8Encoding(false, throwOnInvalidBytes: true).GetCharCount(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static byte[] GetSectionBytes(ReadOnlySpan<byte> data, IndexEntry entry)
    {
        var section = data.Slice(entry.Offset, entry.Length);
        // The length includes the section's terminating NUL.
        return (section.Length > 0 && section[^1] == 0 ? section[..^1] : section).ToArray();
    }

    private static List<IndexEntry> ReadIndex(ReadOnlySpan<byte> data) =>
        TryReadIndex(data, out var index)
            ? index
            : throw new InvalidDataException("The file is not an M3 DS Real cheat database.");

    private static bool TryReadIndex(ReadOnlySpan<byte> data, out List<IndexEntry> index)
    {
        index = [];
        if (data.Length < 4)
        {
            return false;
        }

        var count = BinaryPrimitives.ReadUInt16LittleEndian(data);
        var sectionsStart = 2 + IndexEntryLength * count + NewLine.Length;
        if (data.Length < sectionsStart || !data.Slice(sectionsStart - 2, 2).SequenceEqual("\r\n"u8))
        {
            return false;
        }

        var expectedOffset = sectionsStart;
        for (var n = 0; n < count; n++)
        {
            var entry = data.Slice(2 + IndexEntryLength * n, IndexEntryLength);
            var crc = entry.Slice(7, 8);
            if (!entry.Slice(4, 3).SequenceEqual(IndexTag) || !IsAsciiHex(crc))
            {
                return false;
            }

            var offset = BinaryPrimitives.ReadUInt32LittleEndian(entry[15..]);
            var length = BinaryPrimitives.ReadUInt32LittleEndian(entry[19..]);
            if (offset != expectedOffset || length == 0 || offset + (long)length > data.Length)
            {
                return false;
            }

            var gameId = Encoding.ASCII.GetString(entry[..4]).TrimEnd('?');
            index.Add(new IndexEntry(gameId, Encoding.ASCII.GetString(crc).ToUpperInvariant(), (int)offset, (int)length));
            expectedOffset = (int)(offset + length);
        }

        return true;
    }

    private static bool IsAsciiHex(ReadOnlySpan<byte> bytes)
    {
        foreach (var b in bytes)
        {
            if (!char.IsAsciiHexDigit((char)b))
            {
                return false;
            }
        }

        return true;
    }

    private readonly record struct IndexEntry(string GameId, string Checksum, int Offset, int Length);
}
