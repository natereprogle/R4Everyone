using System.Text;
using Xunit;

namespace R4Everyone.Binary4Everyone.Tests;

public class R4CodecsTests
{
    [Fact]
    public void R4ItemTypes_DetectsFolderAndCheatTypes()
    {
        Assert.True(R4ItemTypes.IsFolder(R4ItemTypes.FolderNormal));
        Assert.True(R4ItemTypes.IsFolder(R4ItemTypes.FolderOneHot));
        Assert.False(R4ItemTypes.IsFolder(R4ItemTypes.CheatEnabled));

        Assert.True(R4ItemTypes.IsCheat(R4ItemTypes.CheatDisabled));
        Assert.True(R4ItemTypes.IsCheat(R4ItemTypes.CheatEnabled));
        Assert.False(R4ItemTypes.IsCheat(R4ItemTypes.FolderNormal));
    }

    [Fact]
    public void R4ItemCodec_WriteAndReadCheat_RoundTripsCoreFields()
    {
        var cheat = TestHelpers.CreateCheat(enabled: false);
        var codec = new R4ItemCodec();
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

        codec.WriteItem(writer, cheat);
        writer.Flush();
        ms.Position = 0;

        using var reader = new BinaryReader(ms, Encoding.ASCII, leaveOpen: true);
        var item = R4ItemCodec.ReadItem(reader);

        var parsed = Assert.IsType<R4Cheat>(item);
        Assert.Equal("C", parsed.Title);
        Assert.Equal(string.Empty, parsed.Description);
        Assert.False(parsed.Enabled);
        Assert.Equal(2, parsed.Code.Count);
    }

    [Fact]
    public void R4ItemCodec_WriteAndReadFolder_RoundTripsHierarchy()
    {
        var folder = new R4Folder
        {
            Title = "F",
            Description = string.Empty,
            OneHot = true
        };
        folder.Items.Add(TestHelpers.CreateCheat());
        var codec = new R4ItemCodec();

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);
        codec.WriteItem(writer, folder);
        writer.Flush();
        ms.Position = 0;

        using var reader = new BinaryReader(ms, Encoding.ASCII, leaveOpen: true);
        var parsed = Assert.IsType<R4Folder>(R4ItemCodec.ReadItem(reader));

        Assert.Equal("F", parsed.Title);
        Assert.Equal(string.Empty, parsed.Description);
        Assert.True(parsed.OneHot);
        Assert.Single(parsed.Items);
        Assert.IsType<R4Cheat>(parsed.Items[0]);
    }

    [Fact]
    public void R4ItemCodec_ReadItem_UnknownType_Throws()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);
        writer.Write((ushort)0);
        writer.Write((ushort)0x2222);
        writer.Write(TestHelpers.BuildAsciiMetaBytes("X", "Y"));
        writer.Write(0);
        writer.Flush();
        ms.Position = 0;
        using var reader = new BinaryReader(ms, Encoding.ASCII, leaveOpen: true);

        Assert.Throws<NotSupportedException>(() => R4ItemCodec.ReadItem(reader));
    }

    [Fact]
    public void R4ItemCodec_ReadItem_IncompleteChunkCount_Throws()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);
        writer.Write((ushort)0);
        writer.Write(R4ItemTypes.CheatEnabled);
        writer.Write(TestHelpers.BuildAsciiMetaBytes("X", "Y"));
        writer.Write(new byte[] { 1, 2, 3 });
        writer.Flush();
        ms.Position = 0;
        using var reader = new BinaryReader(ms, Encoding.ASCII, leaveOpen: true);

        Assert.Throws<InvalidDataException>(() => R4ItemCodec.ReadItem(reader));
    }

    [Fact]
    public void R4ItemCodec_WriteItem_UnknownRuntimeType_Throws()
    {
        var codec = new R4ItemCodec();
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

        Assert.Throws<NotSupportedException>(() => codec.WriteItem(writer, new UnknownItem()));
    }

    [Fact]
    public void R4GameCodec_ReadGameHeader_ThrowsWhenEnabledFlagIsIncomplete()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);
        writer.Write(TestHelpers.BuildAsciiMetaBytes("G", game: true));
        writer.Write((ushort)0);
        writer.Write((byte)0xF0);
        writer.Flush();
        ms.Position = 0;
        using var reader = new BinaryReader(ms, Encoding.ASCII, leaveOpen: true);

        Assert.Throws<InvalidDataException>(() => R4GameCodec.ReadGameHeader(reader, new R4Game("ABCD")));
    }

    [Fact]
    public void R4GameCodec_ReadGameHeader_ThrowsWhenMasterCodeIsIncomplete()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);
        writer.Write(TestHelpers.BuildAsciiMetaBytes("G", game: true));
        writer.Write((ushort)0);
        writer.Write((ushort)0xF000);
        writer.Write(new byte[] { 1, 2, 3 });
        writer.Flush();
        ms.Position = 0;
        using var reader = new BinaryReader(ms, Encoding.ASCII, leaveOpen: true);

        Assert.Throws<InvalidDataException>(() => R4GameCodec.ReadGameHeader(reader, new R4Game("ABCD")));
    }

    [Fact]
    public void R4GameCodec_WriteAndReadGame_RoundTripsCoreData()
    {
        var game = TestHelpers.CreateGame();
        var folder = new R4Folder
        {
            Title = "F",
            Description = string.Empty
        };
        folder.Items.Add(TestHelpers.CreateCheat());
        game.Items.Add(folder);

        var bytes = TestHelpers.SerializeGame(game);
        using var ms = new MemoryStream(bytes);
        using var reader = new BinaryReader(ms, Encoding.ASCII, leaveOpen: true);

        var parsed = R4GameCodec.ReadGame(reader, game.GameId);

        Assert.Equal("T", parsed.GameTitle);
        Assert.True(parsed.GameEnabled);
        Assert.Equal(game.MasterCodes, parsed.MasterCodes);
        Assert.Single(parsed.Items);
        var parsedFolder = Assert.IsType<R4Folder>(parsed.Items[0]);
        Assert.Single(parsedFolder.Items);
        Assert.IsType<R4Cheat>(parsedFolder.Items[0]);
    }

    [Fact]
    public async Task R4DatabaseCodec_SaveAndLoad_Stream_RoundTripsHeaderAndMetadata()
    {
        var db = TestHelpers.CreateDatabaseWithOneGame();
        var codec = new R4DatabaseCodec();
        await using var stream = new MemoryStream();

        await codec.SaveAsync(db, stream);
        stream.Position = 0;
        var loaded = await R4DatabaseCodec.LoadAsync(stream);

        Assert.Equal("UnitTest DB", loaded.Title.TrimEnd('\0'));
        Assert.Equal(R4Encoding.UTF8, loaded.FileEncoding);
        Assert.True(loaded.Enabled);
        Assert.Single(loaded.Games);
        var game = loaded.Games[0];
        Assert.Equal("ABCD", game.GameId);
        Assert.Equal("89ABCDEF", game.GameChecksum);
        Assert.Equal("T", game.GameTitle);
        Assert.True(loaded.LazySlots.ContainsKey(game));
        Assert.Empty(game.Items);
    }

    [Fact]
    public async Task R4DatabaseCodec_SaveAsync_Path_ThrowsWhenPathMissing()
    {
        var db = TestHelpers.CreateDatabaseWithOneGame();
        var codec = new R4DatabaseCodec();

        await Assert.ThrowsAsync<ArgumentException>(() => codec.SaveAsync(db, "Z:\\missing\\missing.dat"));
    }

    [Fact]
    public async Task R4DatabaseCodec_SaveAsync_PathOverride_WritesFileAndUpdatesDatabasePath()
    {
        var db = TestHelpers.CreateDatabaseWithOneGame();
        var codec = new R4DatabaseCodec();
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.dat");
        await File.WriteAllBytesAsync(filePath, [1, 2, 3]);

        try
        {
            await codec.SaveAsync(db, filePath);

            Assert.Equal(filePath, db.R4FilePath);
            var content = await File.ReadAllBytesAsync(filePath);
            Assert.NotEmpty(content);
            Assert.Equal((byte)'R', content[0]);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task R4DatabaseCodec_SaveAsync_Stream_ThrowsForInvalidStream()
    {
        var db = TestHelpers.CreateDatabaseWithOneGame();
        var codec = new R4DatabaseCodec();
        await using var stream = new ReadOnlyNonSeekableStream([1, 2, 3]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => codec.SaveAsync(db, stream));
    }

    [Fact]
    public async Task R4DatabaseCodec_LoadAsync_Stream_ThrowsForInvalidStream()
    {
        await using var stream = new NonReadableSeekableStream([1, 2, 3]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => R4DatabaseCodec.LoadAsync(stream));
    }

    private sealed class UnknownItem : IR4Item
    {
        public string Title { get; set; } = "X";
        public string Description { get; set; } = "Y";
    }

    private sealed class ReadOnlyNonSeekableStream(byte[] buffer) : MemoryStream(buffer)
    {
        public override bool CanWrite => false;
        public override bool CanSeek => false;

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin loc)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class NonReadableSeekableStream(byte[] buffer) : MemoryStream(buffer)
    {
        public override bool CanRead => false;

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
