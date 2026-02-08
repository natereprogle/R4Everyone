using System.Text;

namespace R4Everyone.Binary4Everyone;

internal static class R4ItemTypes
{
    public const ushort CheatDisabled = 0x0000;
    public const ushort CheatEnabled  = 0x0100;
    public const ushort FolderNormal  = 0x1000;
    public const ushort FolderOneHot  = 0x1100;

    public static bool IsFolder(ushort t) => t is FolderNormal or FolderOneHot;
    public static bool IsCheat(ushort t) => t is CheatDisabled or CheatEnabled;
}

internal sealed class R4ItemCodec
{
    public static IR4Item ReadItem(BinaryReader reader)
    {
        // Same layout you currently read:
        // [u16 count/chunks] [u16 type] [meta] [if cheat: u32 chunks] [payload...]
        var numItems = reader.ReadUInt16();
        var itemType = reader.ReadUInt16();
        var meta = R4Binary.ReadItemMeta(reader);

        uint numChunks = 0;
        if (!R4ItemTypes.IsFolder(itemType))
        {
            var chunks = reader.ReadBytes(4);
            if (chunks.Length < 4) throw new InvalidDataException("Incomplete data for item chunk count.");
            numChunks = (uint)(chunks[0] | (chunks[1] << 8) | (chunks[2] << 16) | (chunks[3] << 24));
        }

        if (R4ItemTypes.IsFolder(itemType))
        {
            var folder = new R4Folder
            {
                Title = meta.Title,
                Description = meta.Description ?? "",
                OneHot = itemType == R4ItemTypes.FolderOneHot
            };

            for (var i = 0; i < numItems; i++)
                folder.Items.Add(ReadItem(reader));

            return folder;
        }

        if (R4ItemTypes.IsCheat(itemType))
        {
            return new R4Cheat
            {
                Title = meta.Title,
                Description = meta.Description ?? "",
                Enabled = itemType == R4ItemTypes.CheatEnabled,
                Code = R4Binary.ReadCheatCodes(reader, numChunks)
            };
        }

        throw new NotSupportedException($"Unknown item type encountered: 0x{itemType:X4}");
    }

    public void WriteItem(BinaryWriter writer, IR4Item item)
    {
        switch (item)
        {
            case R4Cheat cheat:
                WriteCheat(writer, cheat);
                return;
            case R4Folder folder:
                WriteFolder(writer, folder);
                return;
            default:
                throw new NotSupportedException($"Unknown item runtime type: {item.GetType().FullName}");
        }
    }

    private static void WriteCheat(BinaryWriter writer, R4Cheat cheat)
    {
        writer.Write((ushort)Math.Min((cheat.Size - 1) / 4, ushort.MaxValue));
        writer.Write(cheat.Enabled ? (ushort)0x0100 : (ushort)0x0000);

        R4Binary.WriteTitleAndDesc(writer, cheat.Title, cheat.Description);

        writer.Write(cheat.Code.Count); // int32

        foreach (var b in cheat.Code.SelectMany(x => x))
            writer.Write(b);
    }

    private void WriteFolder(BinaryWriter writer, R4Folder folder)
    {
        writer.Write((ushort)Math.Min(folder.Items.Count, ushort.MaxValue));
        writer.Write(folder.OneHot ? (ushort)0x1100 : (ushort)0x1000);

        R4Binary.WriteTitleAndDesc(writer, folder.Title, folder.Description);

        foreach (var child in folder.Items)
            WriteItem(writer, child);
    }
}

internal sealed class R4GameCodec
{
    private readonly R4ItemCodec _items = new();

    public static void ReadGameHeader(BinaryReader reader, R4Game game)
    {
        var meta = R4Binary.ReadItemMeta(reader, game: true);
        game.GameTitle = meta.Title;

        reader.ReadUInt16(); // item count (flattened)

        var enabledBytes = reader.ReadBytes(2);
        if (enabledBytes.Length < 2)
            throw new InvalidDataException("Incomplete data for game enabled flag.");
        game.GameEnabled = enabledBytes[1] == 240;

        var masterCodes = new uint[8];
        for (var i = 0; i < masterCodes.Length; i++)
        {
            var bytes = reader.ReadBytes(4);
            if (bytes.Length < 4)
                throw new InvalidDataException("Incomplete data for master code.");
            masterCodes[i] = BitConverter.ToUInt32(bytes);
        }

        game.MasterCodes = masterCodes;
    }

    public static R4Game ReadGame(BinaryReader reader, string gameId)
    {
        var game = new R4Game(gameId)
        {
            GameTitle = R4Binary.ReadItemMeta(reader, game: true).Title
        };

        var numItems = reader.ReadUInt16();
        
        game.GameEnabled = reader.ReadBytes(2)[1] == 240;

        for (var i = 0; i < 8; i++)
            game.MasterCodes[i] = BitConverter.ToUInt32(reader.ReadBytes(4));

        // The game header's numItems is a flattened count (folders include descendants),
        // so we must consume until we hit it.
        var itemsRead = 0;
        while (itemsRead < numItems)
        {
            var item = R4ItemCodec.ReadItem(reader);
            game.Items.Add(item);
            itemsRead += CountFlattened(item);
        }

        return game;
    }

    private static int CountFlattened(IR4Item item)
    {
        var count = 1;
        if (item is not R4Folder f) return count;
        count += f.Items.Sum(CountFlattened);
        return count;
    }

    public void WriteGame(BinaryWriter writer, R4Game game)
    {
        // Exactly matches your serializer behavior: write chars, then seek forward (zero fill).
        writer.Write(game.GameTitle.ToCharArray());
        R4Binary.SeekAlign4WithExtraBlock(writer.BaseStream);

        writer.Write((ushort)Math.Min(game.FlattenedItemCount, ushort.MaxValue));
        writer.Write(game.GameEnabled ? (ushort)0xF000 : (ushort)0x0000);

        foreach (var masterCode in game.MasterCodes)
            writer.Write(masterCode);

        foreach (var item in game.Items)
            _items.WriteItem(writer, item);
    }
}

internal sealed class R4DatabaseCodec
{
    private readonly R4GameCodec _gameCodec = new();

    public static async Task<R4Database> LoadAsync(string filePath)
    {
        await R4Database.ValidateDatabaseAsync(filePath);

        var db = new R4Database { R4FilePath = filePath };
        var snapshot = await File.ReadAllBytesAsync(filePath);
        return LoadFromSnapshot(snapshot, db);
    }

    public static async Task<R4Database> LoadAsync(Stream stream)
    {
        await R4Database.ValidateDatabaseAsync(stream);

        if (!stream.CanRead || !stream.CanSeek)
            throw new InvalidOperationException("Stream must be readable and seekable.");

        stream.Seek(0, SeekOrigin.Begin);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);

        var db = new R4Database();
        return LoadFromSnapshot(ms.ToArray(), db);
    }

    private static R4Database LoadFromSnapshot(byte[] snapshot, R4Database db)
    {
        db.SetSnapshot(snapshot);
        using var stream = new MemoryStream(snapshot, writable: false);
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

        // Header parse – same offsets as your existing ParseDatabaseAsync
        reader.BaseStream.Seek(0x10, SeekOrigin.Begin);
        db.Title = Encoding.ASCII.GetString(reader.ReadBytes(0x4B - 0x10 + 1), 0, 0x4B - 0x10 + 1);

        db.FileEncoding = R4EncodingHelper.GetEncoding(reader.ReadBytes(4));
        db.Enabled = reader.ReadByte() == 1;

        // Game pointer table: [4 gameId][4 checksum][4 offset][4 padding]
        var entries = new List<(string GameId, string Checksum, uint Offset)>();
        reader.BaseStream.Seek(0x100, SeekOrigin.Begin);

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            var chunk = reader.ReadBytes(16);
            if (chunk.Length < 16) break;
            if (chunk.All(b => b == 0x00)) break;

            var gameId = Encoding.ASCII.GetString(chunk.Take(4).ToArray());
            var checksumBytes = chunk.Skip(4).Take(4).ToArray();
            Array.Reverse(checksumBytes);
            var checksum = string.Concat(checksumBytes.Select(b => b.ToString("X2")));
            var offset = BitConverter.ToUInt32(chunk.Skip(8).Take(4).ToArray(), 0);
            entries.Add((gameId, checksum, offset));
        }

        var ordered = entries.OrderBy(e => e.Offset).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var (gameId, checksum, offset) = ordered[i];
            var nextOffset = i + 1 < ordered.Count ? ordered[i + 1].Offset : (uint)snapshot.Length;
            var length = nextOffset >= offset ? nextOffset - offset : 0;

            reader.BaseStream.Seek(offset, SeekOrigin.Begin);
            var game = new R4Game(gameId)
            {
                GameChecksum = checksum
            };
            R4GameCodec.ReadGameHeader(reader, game);
            db.Games.Add(game);
            db.LazySlots[game] = R4LazyGameSlot.CreateExisting(db, game, offset, length);
        }

        return db;
    }

    public async Task SaveAsync(R4Database db, string? filePathOverride = null)
    {
        var path = filePathOverride ?? db.R4FilePath;
        if (!Path.Exists(path) || string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("The file path does not exist");

        db.R4FilePath = path;

        await using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
        await SaveAsync(db, fs);
    }

    public Task SaveAsync(R4Database db, Stream stream)
    {
        if (!stream.CanWrite || !stream.CanSeek)
            throw new InvalidOperationException("Stream must be writable and seekable.");

        try
        {
            stream.SetLength(0);
        }
        catch (NotSupportedException)
        {
            // Not all streams support truncation; we'll overwrite from the start.
        }

        stream.Seek(0, SeekOrigin.Begin);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        // Header
        writer.Write("R4 CheatCode"u8.ToArray());
        writer.Write((short)256);

        writer.Seek(0x10, SeekOrigin.Begin);
        writer.Write(db.Title.ToCharArray());

        writer.Seek(0x4C, SeekOrigin.Begin);
        writer.Write(R4EncodingHelper.GetBytes(db.FileEncoding));

        writer.Write(db.Enabled ? (byte)0x01 : (byte)0x00);

        var writePlans = BuildWritePlans(db);
        var addressBookSize = (uint)(writePlans.Count * 16 + 16);
        var blobRegionStart = 0x100u + addressBookSize;

        var currentOffset = blobRegionStart;
        foreach (var plan in writePlans)
        {
            plan.Offset = currentOffset;
            currentOffset += plan.Length;
        }

        // Address block
        writer.Seek(0x0100, SeekOrigin.Begin);
        foreach (var plan in writePlans)
        {
            var gameIdBytes = Encoding.ASCII.GetBytes(plan.Game.GameId.PadRight(4)[..4]);
            writer.Write(gameIdBytes);

            var checksumBytes = Enumerable.Range(0, 8)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(plan.Game.GameChecksum.Substring(x, 2), 16))
                .ToArray();

            Array.Reverse(checksumBytes);
            writer.Write(checksumBytes);

            writer.Write(plan.Offset);

            var bytesWritten = gameIdBytes.Length + checksumBytes.Length + sizeof(uint);
            var padding = 16 - bytesWritten;
            if (padding > 0)
                writer.Write(new byte[padding]);
        }

        writer.Write(new byte[16]); // terminator row

        // Games
        foreach (var plan in writePlans)
        {
            if (plan.Bytes != null)
            {
                writer.Write(plan.Bytes);
                continue;
            }

            var raw = plan.Slot.GetRawSlice();
            writer.Write(raw.Span);
        }

        writer.Flush();
        return Task.CompletedTask;
    }

    private sealed class GameWritePlan
    {
        public GameWritePlan(R4Game game, R4LazyGameSlot slot, uint length, byte[]? bytes)
        {
            Game = game;
            Slot = slot;
            Length = length;
            Bytes = bytes;
        }

        public R4Game Game { get; }
        public R4LazyGameSlot Slot { get; }
        public uint Length { get; }
        public byte[]? Bytes { get; }
        public uint Offset { get; set; }
    }

    private List<GameWritePlan> BuildWritePlans(R4Database db)
    {
        var plans = new List<GameWritePlan>(db.Games.Count);

        foreach (var game in db.Games)
        {
            var slot = db.GetOrCreateSlot(game);
            if (slot.CanUseRawSource())
            {
                plans.Add(new GameWritePlan(game, slot, slot.SourceLength, bytes: null));
                continue;
            }

            var bytes = SerializeGame(game);
            plans.Add(new GameWritePlan(game, slot, (uint)bytes.Length, bytes));
        }

        return plans;
    }

    private byte[] SerializeGame(R4Game game)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);
        _gameCodec.WriteGame(writer, game);
        writer.Flush();
        return ms.ToArray();
    }
}
