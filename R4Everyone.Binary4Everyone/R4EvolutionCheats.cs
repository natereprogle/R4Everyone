using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace R4Everyone.Binary4Everyone;

/// <summary>
/// The two container variants that share the r4cce CycloDS Evolution reader and writer.
/// </summary>
public enum R4EvolutionVariant
{
    /// <summary>CycloDS Evolution, usually saved as <c>user.evoCheats</c>.</summary>
    CycloDsEvolution,

    /// <summary>EDGE, usually saved as <c>cheats.dat</c>.</summary>
    Edge
}

/// <summary>
/// Reads and writes CycloDS Evolution (<c>.evoCheats</c>) and EDGE (<c>.dat</c>) cheat databases, byte-compatible
/// with r4cce 0.86'.
/// </summary>
/// <remarks>
/// <para>
/// Both formats are an RC4-encrypted header and game index, followed by one record per game. Each record holds a
/// zlib-compressed cheat tree and an ON/OFF block, each protected by an MD5 of its plaintext.
/// </para>
/// <para>
/// These formats store less than an R4 <c>usrcheat.dat</c>. The database title, the database and per-game enable
/// flags, and master codes are not stored: loaded databases use the R4 defaults, with every game enabled. Folders
/// cannot nest, so cheats in nested folders are written into their top-level folder. An unpaired final code word is
/// dropped.
/// </para>
/// </remarks>
public static class R4EvolutionCheats
{
    /// <summary>The most games r4cce will read from one file.</summary>
    public const int MaxGames = 0x1000;

    /// <summary>
    /// The longest code (in rows) the devices are documented to accept. The writer does not enforce it.
    /// </summary>
    public const int MaxCodeRows = 255;

    private const int GameHeaderLength = 0x20;
    private const int IndexEntryLength = 16;
    private const ushort TreeVersion = 4;
    private const int OnOffHeaderLength = 0x11; // MD5 plus the constant 0xC0 byte
    private const byte OnOffGameFlags = 0xC0;
    private const byte OnOffEnabled = 0x80;

    private const ushort FlagFolder = 0x0001;
    private const ushort FlagCode = 0x0002;
    private const ushort FlagNote = 0x0004;
    private const ushort FlagOneHot = 0x0008;

    // r4cce writes "USAY" here; R4Everyone writes its own tag so its files can be recognized.
    private static ReadOnlySpan<byte> CreatorTag => R4CheatFormats.CreatorTag;
    private const int CreatorTagOffset = 0x24;

    private static readonly Layout CycloEvoLayout = new(
        FileMagic: "EvolutionCheats\0"u8.ToArray(),
        OuterHeader: [.. "EvolutionCheats\0"u8, 1, 0, 0, 0, .. new byte[12]],
        EncryptedStart: 0x20,
        InnerHeaderLength: 0x30,
        InnerMagic: [0x01, (byte)'E', (byte)'V', (byte)'O'],
        InnerVersion: 1,
        Key0: Convert.FromHexString("D3BD7C3C5450367E407438A53E1EF140"),
        Key1: Convert.FromHexString("F7877248D34E3672811654A924DF74FD"));

    private static readonly Layout EdgeLayout = new(
        FileMagic: "EDGE\0\0\0\0"u8.ToArray(),
        OuterHeader: "EDGE\0\0\0\0"u8.ToArray(),
        EncryptedStart: 0x08,
        InnerHeaderLength: 0x28,
        InnerMagic: "CHT\0"u8.ToArray(),
        InnerVersion: 0,
        Key0: Convert.FromHexString("08D7B92A4CC591A70E9B249BF7DFD744"),
        Key1: Convert.FromHexString("C14EB9F9B970FFADF1971A1CE558D93E"));

    private sealed record Layout(
        byte[] FileMagic,
        byte[] OuterHeader,
        int EncryptedStart,
        int InnerHeaderLength,
        byte[] InnerMagic,
        uint InnerVersion,
        byte[] Key0,
        byte[] Key1);

    /// <summary>
    /// Returns true if the data starts with the CycloDS Evolution or EDGE magic.
    /// </summary>
    public static bool TryDetect(ReadOnlySpan<byte> data, out R4EvolutionVariant variant)
    {
        if (data.StartsWith(CycloEvoLayout.FileMagic))
        {
            variant = R4EvolutionVariant.CycloDsEvolution;
            return true;
        }

        if (data.StartsWith(EdgeLayout.FileMagic))
        {
            variant = R4EvolutionVariant.Edge;
            return true;
        }

        variant = default;
        return false;
    }

    /// <inheritdoc cref="Load(ReadOnlySpan{byte}, out R4EvolutionVariant)"/>
    public static R4Database Load(ReadOnlySpan<byte> data) => Load(data, out _);

    /// <summary>
    /// Returns true if the file's creator tag says R4Everyone wrote it. r4cce writes <c>USAY</c> instead.
    /// </summary>
    public static bool IsCreatedByR4Everyone(ReadOnlySpan<byte> data)
    {
        if (!TryDetect(data, out var variant))
        {
            return false;
        }

        var layout = GetLayout(variant);
        if (data.Length < layout.EncryptedStart + layout.InnerHeaderLength)
        {
            return false;
        }

        var header = R4Rc4.Apply(layout.Key0, data.Slice(layout.EncryptedStart, layout.InnerHeaderLength));
        return header.AsSpan(CreatorTagOffset, CreatorTag.Length).SequenceEqual(CreatorTag);
    }

    /// <summary>
    /// Reads a CycloDS Evolution or EDGE file into a new, fully materialized database.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// The data is not a CycloDS Evolution or EDGE file, or it is truncated or fails an MD5 check.
    /// </exception>
    public static R4Database Load(ReadOnlySpan<byte> data, out R4EvolutionVariant variant)
    {
        if (!TryDetect(data, out variant))
        {
            throw new InvalidDataException("The file is not a CycloDS Evolution or EDGE cheat database.");
        }

        var layout = GetLayout(variant);
        if (variant == R4EvolutionVariant.CycloDsEvolution &&
            (data.Length < 0x50 || BinaryPrimitives.ReadUInt32LittleEndian(data[0x10..]) != 1))
        {
            throw new InvalidDataException("Unsupported CycloDS Evolution file version.");
        }

        var encStart = layout.EncryptedStart;
        var headerLength = layout.InnerHeaderLength;
        if (data.Length < encStart + headerLength)
        {
            throw new InvalidDataException("The file is too short to contain a header.");
        }

        var header = R4Rc4.Apply(layout.Key0, data.Slice(encStart, headerLength));
        if (!header.AsSpan(0, 4).SequenceEqual(layout.InnerMagic) ||
            BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4)) != layout.InnerVersion)
        {
            throw new InvalidDataException("Unsupported file version (the inner header did not match).");
        }

        var count = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x10));
        if (count > MaxGames || encStart + headerLength + (long)IndexEntryLength * count > data.Length)
        {
            throw new InvalidDataException($"The file's game count ({count}) is invalid.");
        }

        // The index continues the header's keystream, so decrypt both together.
        var plain = R4Rc4.Apply(layout.Key0, data.Slice(encStart, headerLength + IndexEntryLength * (int)count));
        header = plain[..headerLength];
        var index = plain.AsSpan(headerLength);
        if (!R4Md5.HashData(index).AsSpan().SequenceEqual(header.AsSpan(0x14, R4Md5.HashSizeInBytes)))
        {
            throw new InvalidDataException("The game index is corrupt (MD5 mismatch).");
        }

        var db = new R4Database { FileEncoding = R4Encoding.UTF8, Enabled = true };
        for (var n = 0; n < count; n++)
        {
            var entry = index.Slice(n * IndexEntryLength, IndexEntryLength);
            var idBytes = entry[..4];
            var terminator = idBytes.IndexOf((byte)0);
            var gameId = Encoding.ASCII.GetString(terminator >= 0 ? idBytes[..terminator] : idBytes);
            var crc = BinaryPrimitives.ReadUInt32LittleEndian(entry[4..]);
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(entry[12..]);

            var game = ReadGame(data, layout, gameId, crc, offset, n);
            db.Games.Add(game);
            db.MarkGameDirty(game);
        }

        return db;
    }

    /// <summary>
    /// Writes the database as a CycloDS Evolution or EDGE file. Every game is materialized first.
    /// </summary>
    /// <exception cref="InvalidOperationException">The database cannot be represented in this format.</exception>
    /// <exception cref="FormatException">A game's checksum is not 8 hex characters.</exception>
    public static byte[] Save(R4Database db, R4EvolutionVariant variant)
    {
        ArgumentNullException.ThrowIfNull(db);
        db.MaterializeAllGames();

        if (db.Games.Count > MaxGames)
        {
            throw new InvalidOperationException(
                $"{GetFormatName(variant)} files can hold at most {MaxGames} games, but this file has {db.Games.Count}.");
        }

        var layout = GetLayout(variant);
        var gameCount = db.Games.Count;
        var encStart = layout.EncryptedStart;
        var indexStart = encStart + layout.InnerHeaderLength;

        using var output = new MemoryStream();
        output.Write(layout.OuterHeader);
        output.Write(new byte[layout.InnerHeaderLength + IndexEntryLength * gameCount]);

        var index = new byte[IndexEntryLength * gameCount];
        for (var n = 0; n < gameCount; n++)
        {
            var game = db.Games[n];
            var recordOffset = checked((uint)output.Length);
            var crc = ParseChecksum(game);

            var tree = EncodeTree(game, crc);
            var compressed = Compress(tree);
            var onOff = EncodeOnOff(game);

            var gameHeader = new byte[GameHeaderLength];
            BinaryPrimitives.WriteUInt32LittleEndian(gameHeader, (uint)compressed.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(gameHeader.AsSpan(4), (uint)tree.Length);
            R4Md5.HashData(compressed).CopyTo(gameHeader, 8);
            BinaryPrimitives.WriteUInt32LittleEndian(gameHeader.AsSpan(0x18),
                checked(recordOffset + GameHeaderLength + (uint)compressed.Length));
            BinaryPrimitives.WriteUInt32LittleEndian(gameHeader.AsSpan(0x1C), (uint)onOff.Length);

            output.Write(R4Rc4.Apply(layout.Key0, gameHeader));
            output.Write(R4Rc4.Apply(layout.Key1, compressed));
            output.Write(R4Rc4.Apply(layout.Key1, onOff));

            var entry = index.AsSpan(n * IndexEntryLength, IndexEntryLength);
            GetGameIdBytes(game).CopyTo(entry);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[4..], crc);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[12..], recordOffset);
        }

        var header = new byte[layout.InnerHeaderLength];
        layout.InnerMagic.CopyTo(header, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), layout.InnerVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x10), (uint)gameCount);
        R4Md5.HashData(index).CopyTo(header, 0x14);
        CreatorTag.CopyTo(header.AsSpan(CreatorTagOffset));

        var file = output.ToArray();
        var encrypted = R4Rc4.Apply(layout.Key0, [.. header, .. index]);
        encrypted.CopyTo(file, encStart);
        return file;
    }

    /// <summary>
    /// Returns the cheats that are longer than <see cref="MaxCodeRows"/> rows, which the devices may not accept.
    /// </summary>
    public static IEnumerable<(R4Game Game, R4Cheat Cheat)> FindOverlongCheats(R4Database db)
    {
        db.MaterializeAllGames();
        foreach (var game in db.Games)
        {
            foreach (var cheat in EnumerateCheats(game.Items))
            {
                if (cheat.Code.Count / 2 > MaxCodeRows)
                {
                    yield return (game, cheat);
                }
            }
        }
    }

    private static Layout GetLayout(R4EvolutionVariant variant) => variant switch
    {
        R4EvolutionVariant.CycloDsEvolution => CycloEvoLayout,
        R4EvolutionVariant.Edge => EdgeLayout,
        _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, null)
    };

    private static string GetFormatName(R4EvolutionVariant variant) =>
        variant == R4EvolutionVariant.Edge ? "EDGE" : "CycloDS Evolution";

    private static R4Game ReadGame(ReadOnlySpan<byte> data, Layout layout, string gameId, uint crc, uint offset, int n)
    {
        var context = $"Game {n + 1} ({gameId})";
        var gameHeader = R4Rc4.Apply(layout.Key0, Slice(data, offset, GameHeaderLength, context));
        var compressedSize = BinaryPrimitives.ReadUInt32LittleEndian(gameHeader);
        var rawSize = BinaryPrimitives.ReadUInt32LittleEndian(gameHeader.AsSpan(4));
        var onOffOffset = BinaryPrimitives.ReadUInt32LittleEndian(gameHeader.AsSpan(0x18));
        var onOffSize = BinaryPrimitives.ReadUInt32LittleEndian(gameHeader.AsSpan(0x1C));

        var compressed = R4Rc4.Apply(layout.Key1,
            Slice(data, (long)offset + GameHeaderLength, compressedSize, context));
        if (!R4Md5.HashData(compressed).AsSpan().SequenceEqual(gameHeader.AsSpan(8, R4Md5.HashSizeInBytes)))
        {
            throw new InvalidDataException($"{context} is corrupt (payload MD5 mismatch).");
        }

        var tree = Decompress(compressed, rawSize, context);

        if (onOffSize < R4Md5.HashSizeInBytes)
        {
            throw new InvalidDataException($"{context} has an invalid ON/OFF block.");
        }

        var onOff = R4Rc4.Apply(layout.Key1, Slice(data, onOffOffset, onOffSize, context));
        if (!R4Md5.HashData(onOff.AsSpan(R4Md5.HashSizeInBytes)).AsSpan()
                .SequenceEqual(onOff.AsSpan(0, R4Md5.HashSizeInBytes)))
        {
            throw new InvalidDataException($"{context} is corrupt (ON/OFF MD5 mismatch).");
        }

        var flags = onOff.Length > OnOffHeaderLength ? onOff[OnOffHeaderLength..] : [];
        return DecodeTree(tree, gameId, crc, flags, context);
    }

    private static ReadOnlySpan<byte> Slice(ReadOnlySpan<byte> data, long offset, long length, string context)
    {
        if (offset < 0 || length < 0 || offset + length > data.Length)
        {
            throw new InvalidDataException($"{context} points outside the file.");
        }

        return data.Slice((int)offset, (int)length);
    }

    private static byte[] Compress(byte[] data)
    {
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(data);
        }

        return compressed.ToArray();
    }

    private static byte[] Decompress(byte[] compressed, uint rawSize, string context)
    {
        try
        {
            using var zlib = new ZLibStream(new MemoryStream(compressed, writable: false), CompressionMode.Decompress);
            var buffer = new byte[rawSize];
            zlib.ReadExactly(buffer);
            if (zlib.ReadByte() != -1)
            {
                throw new InvalidDataException($"{context} is larger than its header says.");
            }

            return buffer;
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidDataException($"{context} is smaller than its header says.", ex);
        }
    }

    private static R4Game DecodeTree(byte[] tree, string gameId, uint crc, ReadOnlySpan<byte> onOffFlags,
        string context)
    {
        var reader = new TreeReader(tree, context);
        if (reader.ReadUInt16() != TreeVersion)
        {
            throw new InvalidDataException($"{context} uses an unsupported cheat list version.");
        }

        var itemCount = reader.ReadUInt16();
        reader.ReadUInt32(); // tree size, ignored like r4cce does

        var game = new R4Game(gameId)
        {
            GameTitle = reader.ReadString(),
            GameChecksum = crc.ToString("X8", CultureInfo.InvariantCulture),
            GameEnabled = true
        };
        reader.ReadString(); // "ID CRC|timestamp", ignored like r4cce does

        var codeIndex = 0;
        for (var i = 0; i < itemCount; i++)
        {
            var flags = reader.PeekUInt16();
            if ((flags & FlagFolder) == 0)
            {
                game.Items.Add(ReadCode(ref reader, onOffFlags, ref codeIndex));
                continue;
            }

            if ((flags & FlagCode) != 0)
            {
                throw new InvalidDataException($"{context} has an item marked as both a folder and a code.");
            }

            reader.ReadUInt16();
            var codeCount = reader.ReadUInt16();
            reader.ReadUInt32(); // folder size
            var folder = new R4Folder
            {
                Title = reader.ReadString(),
                Description = (flags & FlagNote) != 0 ? reader.ReadString() : string.Empty,
                OneHot = (flags & FlagOneHot) != 0
            };

            for (var c = 0; c < codeCount; c++)
            {
                folder.Items.Add(ReadCode(ref reader, onOffFlags, ref codeIndex));
            }

            game.Items.Add(folder);
        }

        return game;
    }

    private static R4Cheat ReadCode(ref TreeReader reader, ReadOnlySpan<byte> onOffFlags, ref int codeIndex)
    {
        var flags = reader.ReadUInt16();
        if ((flags & FlagFolder) != 0 || (flags & FlagCode) == 0 || (flags & FlagOneHot) != 0)
        {
            throw new InvalidDataException($"{reader.Context} has a code with invalid flags 0x{flags:X4}.");
        }

        var pairCount = reader.ReadUInt16();
        var cheat = new R4Cheat
        {
            Title = reader.ReadString(),
            Description = (flags & FlagNote) != 0 ? reader.ReadString() : string.Empty,
            Enabled = codeIndex < onOffFlags.Length && onOffFlags[codeIndex] != 0
        };
        codeIndex++;

        var words = new List<byte[]>(pairCount * 2);
        for (var i = 0; i < pairCount * 2; i++)
        {
            words.Add(reader.ReadBytes(4));
        }

        cheat.Code = words;
        return cheat;
    }

    private static byte[] EncodeTree(R4Game game, uint crc)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
        var context = $"Game '{game.GameTitle}'";

        writer.Write(TreeVersion);
        writer.Write(CheckedU16(game.Items.Count, $"{context} has too many top-level items"));
        writer.Write(0u); // tree size, patched below
        WriteString(writer, game.GameTitle);
        writer.Write(GetGameIdBytes(game));
        writer.Write(Encoding.ASCII.GetBytes($" {crc:X8}|0000/00/00 00:00\0"));

        foreach (var item in game.Items)
        {
            switch (item)
            {
                case R4Folder folder:
                    var start = ms.Position;
                    var codes = EnumerateCheats(folder.Items).ToList();
                    var flags = (ushort)(FlagFolder
                                         | (HasNote(folder.Description) ? FlagNote : 0)
                                         | (folder.OneHot ? FlagOneHot : 0));
                    writer.Write(flags);
                    writer.Write(CheckedU16(codes.Count, $"Folder '{folder.Title}' has too many cheats"));
                    writer.Write(0u); // folder size, patched below
                    WriteString(writer, folder.Title);
                    if (HasNote(folder.Description))
                    {
                        WriteString(writer, folder.Description);
                    }

                    foreach (var code in codes)
                    {
                        WriteCode(writer, code);
                    }

                    PatchUInt32(ms, start + 4, (uint)(ms.Position - start));
                    break;
                case R4Cheat cheat:
                    WriteCode(writer, cheat);
                    break;
            }
        }

        writer.Flush();
        PatchUInt32(ms, 4, (uint)ms.Length);
        return ms.ToArray();
    }

    private static void WriteCode(BinaryWriter writer, R4Cheat cheat)
    {
        var pairCount = CheckedU16(cheat.Code.Count / 2, $"Cheat '{cheat.Title}' is too long");
        writer.Write((ushort)(FlagCode | (HasNote(cheat.Description) ? FlagNote : 0)));
        writer.Write(pairCount);
        WriteString(writer, cheat.Title);
        if (HasNote(cheat.Description))
        {
            WriteString(writer, cheat.Description);
        }

        // An unpaired final word is dropped, as r4cce does.
        for (var i = 0; i < pairCount * 2; i++)
        {
            var word = cheat.Code[i];
            if (word.Length != 4)
            {
                throw new InvalidOperationException($"Cheat '{cheat.Title}' has a code block that is not 4 bytes.");
            }

            writer.Write(word);
        }
    }

    private static byte[] EncodeOnOff(R4Game game)
    {
        var block = new List<byte>(new byte[R4Md5.HashSizeInBytes]) { OnOffGameFlags };
        foreach (var item in game.Items)
        {
            var cheats = item switch
            {
                R4Folder folder => EnumerateCheats(folder.Items),
                R4Cheat cheat => [cheat],
                _ => []
            };

            block.AddRange(cheats.Select(cheat => cheat.Enabled ? OnOffEnabled : (byte)0));
        }

        block.AddRange(new byte[4]);
        while (block.Count % 4 != 0)
        {
            block.Add(0);
        }

        var bytes = block.ToArray();
        R4Md5.HashData(bytes.AsSpan(R4Md5.HashSizeInBytes)).CopyTo(bytes, 0);
        return bytes;
    }

    // Folders cannot nest in this format, so a folder's nested cheats are flattened into it in tree order.
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

    private static bool HasNote(string? note) => !string.IsNullOrEmpty(note);

    private static void WriteString(BinaryWriter writer, string? value)
    {
        // An embedded NUL would end the string early and corrupt the rest of the tree.
        writer.Write(Encoding.UTF8.GetBytes((value ?? string.Empty).Replace("\0", string.Empty)));
        writer.Write((byte)0);
    }

    private static void PatchUInt32(MemoryStream ms, long position, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(ms.GetBuffer().AsSpan((int)position, 4), value);
    }

    private static ushort CheckedU16(int value, string message) =>
        value <= ushort.MaxValue ? (ushort)value : throw new InvalidOperationException($"{message} ({value}).");

    // Matches the R4 writer: short IDs are padded with spaces rather than leaking NULs into the file.
    private static byte[] GetGameIdBytes(R4Game game) =>
        Encoding.ASCII.GetBytes((game.GameId ?? string.Empty).PadRight(4)[..4]);

    internal static uint ParseChecksum(R4Game game)
    {
        if (game.GameChecksum is { Length: 8 } checksum &&
            uint.TryParse(checksum, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var crc))
        {
            return crc;
        }

        throw new FormatException($"Game '{game.GameTitle}' checksum must be 8 hex characters.");
    }

    private ref struct TreeReader(ReadOnlySpan<byte> data, string context)
    {
        private readonly ReadOnlySpan<byte> _data = data;
        private int _position;

        public string Context { get; } = context;

        public ushort PeekUInt16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2, advance: false));

        public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2));

        public uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));

        public byte[] ReadBytes(int count) => Take(count).ToArray();

        public string ReadString()
        {
            var end = _position < _data.Length ? _data[_position..].IndexOf((byte)0) : -1;
            if (end < 0)
            {
                throw new InvalidDataException($"{Context} has an unterminated string.");
            }

            var value = Encoding.UTF8.GetString(_data.Slice(_position, end));
            _position += end + 1;
            return value;
        }

        private ReadOnlySpan<byte> Take(int count, bool advance = true)
        {
            if (_position + count > _data.Length)
            {
                throw new InvalidDataException($"{Context}'s cheat list ends unexpectedly.");
            }

            var slice = _data.Slice(_position, count);
            if (advance)
            {
                _position += count;
            }

            return slice;
        }
    }
}
