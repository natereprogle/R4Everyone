using System.Text;
using Xunit;

namespace R4Everyone.Binary4Everyone.Tests;

public class R4BinaryTests
{
    [Fact]
    public void SeekAlign4WithExtraBlock_AlignedPosition_AddsExtraFourBytes()
    {
        using var ms = new MemoryStream(new byte[32]) { Position = 8 };

        R4Binary.SeekAlign4WithExtraBlock(ms);

        Assert.Equal(12, ms.Position);
    }

    [Fact]
    public void SeekAlign4WithExtraBlock_UnalignedPosition_SeeksToNextBoundary()
    {
        using var ms = new MemoryStream(new byte[32]) { Position = 9 };

        R4Binary.SeekAlign4WithExtraBlock(ms);

        Assert.Equal(12, ms.Position);
    }

    [Fact]
    public void ReadItemMeta_GameMode_ReadsTitleAndNullDescription()
    {
        var bytes = TestHelpers.BuildAsciiMetaBytes("AB", game: true);
        using var ms = new MemoryStream(bytes);
        using var reader = new BinaryReader(ms, Encoding.ASCII, leaveOpen: true);

        var (title, description) = R4Binary.ReadItemMeta(reader, game: true);

        Assert.Equal("AB", title);
        Assert.Null(description);
        Assert.Equal(4, ms.Position);
    }

    [Fact]
    public void ReadItemMeta_NormalMode_ReadsTitleAndDescription()
    {
        var bytes = TestHelpers.BuildAsciiMetaBytes("A", "BC");
        using var ms = new MemoryStream(bytes);
        using var reader = new BinaryReader(ms, Encoding.ASCII, leaveOpen: true);

        var (title, description) = R4Binary.ReadItemMeta(reader);

        Assert.Equal("A", title);
        Assert.Equal("BC", description);
        Assert.Equal(8, ms.Position);
    }

    [Fact]
    public void ReadCheatCodes_ZeroChunks_ReturnsEmptyList()
    {
        using var ms = new MemoryStream();
        using var reader = new BinaryReader(ms);

        var codes = R4Binary.ReadCheatCodes(reader, 0);

        Assert.Empty(codes);
    }

    [Fact]
    public void ReadCheatCodes_ReadsRequestedChunks()
    {
        using var ms = new MemoryStream([1, 2, 3, 4, 5, 6, 7, 8]);
        using var reader = new BinaryReader(ms);

        var codes = R4Binary.ReadCheatCodes(reader, 2);

        Assert.Equal(2, codes.Count);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, codes[0]);
        Assert.Equal(new byte[] { 5, 6, 7, 8 }, codes[1]);
    }

    [Fact]
    public void ReadCheatCodes_IncompleteChunk_Throws()
    {
        using var ms = new MemoryStream([1, 2, 3, 4, 5]);
        using var reader = new BinaryReader(ms);

        Assert.Throws<InvalidDataException>(() => R4Binary.ReadCheatCodes(reader, 2));
    }

    [Fact]
    public void WriteTitleAndDesc_WritesSeparatorAndAlignsPosition()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

        R4Binary.WriteTitleAndDesc(writer, "A", "B");

        var bytes = ms.ToArray();
        Assert.Equal(4, ms.Position);
        Assert.Equal(3, bytes.Length);
        Assert.Equal((byte)'A', bytes[0]);
        Assert.Equal(0, bytes[1]);
        Assert.Equal((byte)'B', bytes[2]);
    }
}
