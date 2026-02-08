using System;
using System.IO;

namespace R4Everyone.Binary4Everyone;

internal sealed class R4LazyGameSlot
{
    private readonly R4Database _database;

    public R4Game Game { get; }
    public uint SourceOffset { get; }
    public uint SourceLength { get; }
    public bool IsDirty { get; private set; }
    public bool IsMaterialized { get; private set; }

    private R4LazyGameSlot(R4Database database, R4Game game, uint sourceOffset, uint sourceLength)
    {
        _database = database;
        Game = game;
        SourceOffset = sourceOffset;
        SourceLength = sourceLength;
    }

    public static R4LazyGameSlot CreateExisting(R4Database database, R4Game game, uint sourceOffset, uint sourceLength)
        => new(database, game, sourceOffset, sourceLength);

    public static R4LazyGameSlot CreateNew(R4Database database, R4Game game)
    {
        var slot = new R4LazyGameSlot(database, game, 0, 0) { IsDirty = true, IsMaterialized = true };
        return slot;
    }

    public void EnsureMaterialized()
    {
        if (IsMaterialized)
        {
            return;
        }

        var snapshot = _database.SnapshotBytes;
        if (snapshot == null || SourceLength == 0)
        {
            IsMaterialized = true;
            return;
        }

        using var stream = new MemoryStream(snapshot, writable: false);
        using var reader = new BinaryReader(stream);
        stream.Seek(SourceOffset, SeekOrigin.Begin);

        var parsed = R4GameCodec.ReadGame(reader, Game.GameId);

        ApplyParsedGame(Game, parsed);
        IsMaterialized = true;
    }

    public void MarkDirty()
    {
        IsDirty = true;
    }

    public bool CanUseRawSource()
    {
        return !IsDirty && SourceLength > 0 && _database.SnapshotBytes != null;
    }

    public ReadOnlyMemory<byte> GetRawSlice()
    {
        var snapshot = _database.SnapshotBytes ?? Array.Empty<byte>();
        return snapshot.AsMemory((int)SourceOffset, (int)SourceLength);
    }

    private static void ApplyParsedGame(R4Game target, R4Game parsed)
    {
        target.GameTitle = parsed.GameTitle;
        target.GameEnabled = parsed.GameEnabled;
        target.MasterCodes = parsed.MasterCodes;

        target.Items.Clear();
        foreach (var item in parsed.Items)
        {
            target.Items.Add(item);
        }
    }
}
