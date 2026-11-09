using System.Buffers.Binary;
using System.Text;
using Xunit;

namespace R4Everyone.Binary4Everyone.Tests;

public class R4EvolutionCheatsTests
{
    private static readonly byte[] CycloKey0 = Convert.FromHexString("D3BD7C3C5450367E407438A53E1EF140");
    private static readonly byte[] CycloKey1 = Convert.FromHexString("F7877248D34E3672811654A924DF74FD");

    public static TheoryData<R4EvolutionVariant> Variants => [R4EvolutionVariant.CycloDsEvolution, R4EvolutionVariant.Edge];

    [Theory]
    [InlineData(R4EvolutionVariant.CycloDsEvolution)]
    [InlineData(R4EvolutionVariant.Edge)]
    public void Load_ReadsFilesWrittenByReferenceImplementation(R4EvolutionVariant variant)
    {
        var file = variant == R4EvolutionVariant.Edge ? ReferenceFixtures.Edge : ReferenceFixtures.CycloEvo;

        var db = R4EvolutionCheats.Load(file, out var detected);

        Assert.Equal(variant, detected);
        ReferenceFixtures.AssertEquivalent(ReferenceFixtures.BuildDatabase(), db);
    }

    [Fact]
    public void Load_UsesDefaultsForFieldsTheFormatDoesNotStore()
    {
        var db = R4EvolutionCheats.Load(ReferenceFixtures.CycloEvo);

        Assert.Equal(new R4Database().Title, db.Title);
        Assert.Equal(R4Encoding.UTF8, db.FileEncoding);
        Assert.True(db.Enabled);
        Assert.All(db.Games, game =>
        {
            Assert.True(game.GameEnabled);
            Assert.Equal(new R4Game("x").MasterCodes, game.MasterCodes);
        });
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void SaveThenLoad_RoundTrips(R4EvolutionVariant variant)
    {
        var expected = ReferenceFixtures.BuildDatabase();

        var bytes = R4EvolutionCheats.Save(ReferenceFixtures.BuildDatabase(), variant);
        var loaded = R4EvolutionCheats.Load(bytes, out var detected);

        Assert.Equal(variant, detected);
        ReferenceFixtures.AssertEquivalent(expected, loaded);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void Save_MatchesReferenceImplementationOutsideCompressedData(R4EvolutionVariant variant)
    {
        // zlib output can differ between implementations, so compare everything in the file except each game's
        // payload: the headers, index, game headers, and ON/OFF blocks.
        var reference = variant == R4EvolutionVariant.Edge ? ReferenceFixtures.Edge : ReferenceFixtures.CycloEvo;
        var ours = R4EvolutionCheats.Save(ReferenceFixtures.BuildDatabase(), variant);

        Assert.Equal(Decrypt(reference, variant), Decrypt(ours, variant));
    }

    [Fact]
    public void Save_WritesDocumentedOuterAndInnerHeaders()
    {
        var file = R4EvolutionCheats.Save(ReferenceFixtures.BuildDatabase(), R4EvolutionVariant.CycloDsEvolution);

        Assert.Equal("EvolutionCheats\0"u8.ToArray(), file[..16]);
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(0x10)));
        Assert.All(file[0x14..0x20], b => Assert.Equal(0, b));

        var plain = R4Rc4.Apply(CycloKey0, file.AsSpan(0x20, 0x30 + 2 * 16));
        Assert.Equal(new byte[] { 0x01, (byte)'E', (byte)'V', (byte)'O' }, plain[..4]);
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(plain.AsSpan(4)));
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(plain.AsSpan(0x10)));
        Assert.Equal(R4Md5.HashData(plain.AsSpan(0x30)), plain[0x14..0x24]);
        Assert.Equal("R4EO"u8.ToArray(), plain[0x24..0x28]);

        var index = plain.AsSpan(0x30);
        Assert.Equal("ASME"u8.ToArray(), index[..4].ToArray());
        Assert.Equal(0x2F00B549u, BinaryPrimitives.ReadUInt32LittleEndian(index[4..]));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(index[8..]));
        Assert.Equal(0x50u + 2 * 16, BinaryPrimitives.ReadUInt32LittleEndian(index[12..]));
    }

    [Fact]
    public void Save_EdgeHeaderStartsAtOffset8()
    {
        var file = R4EvolutionCheats.Save(new R4Database(), R4EvolutionVariant.Edge);

        Assert.Equal("EDGE\0\0\0\0"u8.ToArray(), file[..8]);
        Assert.Equal(8 + 0x28, file.Length);
        Assert.Empty(R4EvolutionCheats.Load(file).Games);
    }

    [Fact]
    public void Save_WritesTreeAndOnOffBlockAsDocumented()
    {
        var file = R4EvolutionCheats.Save(ReferenceFixtures.BuildDatabase(), R4EvolutionVariant.CycloDsEvolution);
        var (tree, onOff) = ReadFirstGame(file);

        Assert.Equal(4, BinaryPrimitives.ReadUInt16LittleEndian(tree));
        Assert.Equal(3, BinaryPrimitives.ReadUInt16LittleEndian(tree.AsSpan(2)));
        Assert.Equal((uint)tree.Length, BinaryPrimitives.ReadUInt32LittleEndian(tree.AsSpan(4)));
        Assert.Contains("Super Mario 64 DS\0ASME 2F00B549|0000/00/00 00:00\0", Encoding.UTF8.GetString(tree));

        // MD5, 0xC0, then one byte per code in tree order (99 Lives, Moon Jump, Speed, Tail), then 4 zeros, padded.
        Assert.Equal(R4Md5.HashData(onOff.AsSpan(16)), onOff[..16]);
        Assert.Equal(new byte[] { 0xC0, 0x80, 0x80, 0x00, 0x00, 0, 0, 0, 0, 0, 0, 0 }, onOff[16..]);
        Assert.Equal(0, onOff.Length % 4);
    }

    [Fact]
    public void Save_FolderFlagsAndSizeMatchSpec()
    {
        var db = new R4Database();
        var game = new R4Game("ABCD") { GameTitle = "G", GameChecksum = "00000000" };
        var folder = new R4Folder { Title = "F", Description = "N", OneHot = true };
        folder.Items.Add(new R4Cheat { Title = "C", Description = "", Code = ReferenceFixtures.Words(1, 2) });
        game.Items.Add(folder);
        db.Games.Add(game);

        var (tree, _) = ReadFirstGame(R4EvolutionCheats.Save(db, R4EvolutionVariant.Edge));

        var folderStart = 8 + "G\0ABCD 00000000|0000/00/00 00:00\0".Length;
        Assert.Equal(0x0D, BinaryPrimitives.ReadUInt16LittleEndian(tree.AsSpan(folderStart)));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(tree.AsSpan(folderStart + 2)));
        Assert.Equal((uint)(tree.Length - folderStart), BinaryPrimitives.ReadUInt32LittleEndian(tree.AsSpan(folderStart + 4)));
        var codeStart = folderStart + 8 + "F\0N\0".Length;
        Assert.Equal(0x02, BinaryPrimitives.ReadUInt16LittleEndian(tree.AsSpan(codeStart)));
    }

    [Fact]
    public void Save_FlattensNestedFoldersAndDropsUnpairedWord()
    {
        var db = new R4Database();
        var game = new R4Game("ABCD") { GameTitle = "G", GameChecksum = "00000000" };
        var outer = new R4Folder { Title = "Outer", Description = "" };
        var inner = new R4Folder { Title = "Inner", Description = "" };
        inner.Items.Add(new R4Cheat { Title = "Deep", Description = "", Enabled = true, Code = ReferenceFixtures.Words(1, 2, 3) });
        outer.Items.Add(new R4Cheat { Title = "Shallow", Description = "", Code = ReferenceFixtures.Words(4, 5) });
        outer.Items.Add(inner);
        game.Items.Add(outer);
        db.Games.Add(game);

        var loaded = R4EvolutionCheats.Load(R4EvolutionCheats.Save(db, R4EvolutionVariant.CycloDsEvolution));

        var folder = Assert.IsType<R4Folder>(Assert.Single(loaded.Games[0].Items));
        Assert.Equal(["Shallow", "Deep"], folder.Items.Select(i => ((R4Cheat)i).Title));
        var deep = (R4Cheat)folder.Items[1];
        Assert.True(deep.Enabled);
        Assert.Equal(ReferenceFixtures.Words(1, 2), deep.Code);
    }

    [Fact]
    public void Save_PadsShortGameIdWithSpaces()
    {
        var db = new R4Database();
        db.Games.Add(new R4Game("AB") { GameTitle = "G", GameChecksum = "00000000" });

        var loaded = R4EvolutionCheats.Load(R4EvolutionCheats.Save(db, R4EvolutionVariant.Edge));

        Assert.Equal("AB  ", loaded.Games[0].GameId);
        Assert.Equal("G", loaded.Games[0].GameTitle);
    }

    [Fact]
    public async Task Save_MaterializesLazyR4Games()
    {
        var source = ReferenceFixtures.BuildAsciiDatabase();
        using var r4 = new MemoryStream();
        await R4CheatDat.SaveAsync(source, r4);
        r4.Position = 0;
        var lazy = await R4CheatDat.LoadAsync(r4);

        var loaded = R4EvolutionCheats.Load(R4EvolutionCheats.Save(lazy, R4EvolutionVariant.CycloDsEvolution));

        ReferenceFixtures.AssertEquivalent(source, loaded);
    }

    [Fact]
    public void Save_InvalidChecksum_Throws()
    {
        var db = new R4Database();
        db.Games.Add(new R4Game("ABCD") { GameChecksum = "XYZ" });

        Assert.Throws<FormatException>(() => R4EvolutionCheats.Save(db, R4EvolutionVariant.Edge));
    }

    [Fact]
    public void Save_TooManyGames_Throws()
    {
        var db = new R4Database();
        for (var i = 0; i <= R4EvolutionCheats.MaxGames; i++)
        {
            db.Games.Add(new R4Game("ABCD") { GameChecksum = "00000000" });
        }

        Assert.Throws<InvalidOperationException>(() => R4EvolutionCheats.Save(db, R4EvolutionVariant.Edge));
    }

    [Fact]
    public void FindOverlongCheats_ReportsCheatsOver255Rows()
    {
        var db = new R4Database();
        var game = new R4Game("ABCD") { GameChecksum = "00000000" };
        game.Items.Add(new R4Cheat { Title = "Ok", Code = ReferenceFixtures.Words(new uint[255 * 2]) });
        game.Items.Add(new R4Cheat { Title = "Long", Code = ReferenceFixtures.Words(new uint[256 * 2]) });
        db.Games.Add(game);

        Assert.Equal("Long", Assert.Single(R4EvolutionCheats.FindOverlongCheats(db)).Cheat.Title);
    }

    [Theory]
    [InlineData(0x34, "game index is corrupt")] // index MD5 in the inner header
    [InlineData(0x60, "game index is corrupt")] // index entry
    public void Load_TamperedIndex_Throws(int offset, string message)
    {
        var file = ReferenceFixtures.CycloEvo.ToArray();
        file[offset] ^= 0xFF;

        var ex = Assert.Throws<InvalidDataException>(() => R4EvolutionCheats.Load(file));
        Assert.Contains(message, ex.Message);
    }

    [Fact]
    public void Load_TamperedPayload_Throws()
    {
        var file = ReferenceFixtures.CycloEvo.ToArray();
        file[0x70 + 0x20 + 3] ^= 0x01; // inside the first game's compressed payload

        var ex = Assert.Throws<InvalidDataException>(() => R4EvolutionCheats.Load(file));
        Assert.Contains("payload MD5 mismatch", ex.Message);
    }

    [Fact]
    public void Load_TamperedOnOffBlock_Throws()
    {
        var file = ReferenceFixtures.CycloEvo.ToArray();
        var header = R4Rc4.Apply(CycloKey0, file.AsSpan(0x70, 0x20));
        var onOffOffset = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0x18));
        file[onOffOffset + 17] ^= 0x80;

        var ex = Assert.Throws<InvalidDataException>(() => R4EvolutionCheats.Load(file));
        Assert.Contains("ON/OFF MD5 mismatch", ex.Message);
    }

    [Fact]
    public void Load_TruncatedFile_Throws()
    {
        var file = ReferenceFixtures.Edge[..^10];

        Assert.Throws<InvalidDataException>(() => R4EvolutionCheats.Load(file));
    }

    [Theory]
    [InlineData(new byte[] { 0x52, 0x34 })]
    [InlineData(new byte[] { 0x45, 0x44, 0x47, 0x45, 0x00, 0x00, 0x00, 0x01 })]
    public void Load_UnknownMagic_Throws(byte[] file)
    {
        Assert.False(R4EvolutionCheats.TryDetect(file, out _));
        Assert.Throws<InvalidDataException>(() => R4EvolutionCheats.Load(file));
    }

    [Fact]
    public void Load_CycloEvoWithWrongOuterVersion_Throws()
    {
        var file = ReferenceFixtures.CycloEvo.ToArray();
        file[0x10] = 2;

        var ex = Assert.Throws<InvalidDataException>(() => R4EvolutionCheats.Load(file));
        Assert.Contains("version", ex.Message);
    }

    [Fact]
    public void Load_ShortOnOffBlock_LeavesRemainingCodesOff()
    {
        // Build a single-game file by hand whose ON/OFF block only covers the first code.
        var db = new R4Database();
        var game = new R4Game("ABCD") { GameTitle = "G", GameChecksum = "00000000" };
        game.Items.Add(new R4Cheat { Title = "A", Enabled = true, Code = [] });
        game.Items.Add(new R4Cheat { Title = "B", Enabled = true, Code = [] });
        db.Games.Add(game);
        var file = R4EvolutionCheats.Save(db, R4EvolutionVariant.CycloDsEvolution);

        var recordOffset = 0x50 + 16;
        var header = R4Rc4.Apply(CycloKey0, file.AsSpan(recordOffset, 0x20));
        var onOffOffset = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0x18));
        byte[] body = [0xC0, 0x80];
        byte[] block = [.. R4Md5.HashData(body), .. body];
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0x1C), block.Length);
        var patched = file[..onOffOffset].Concat(R4Rc4.Apply(CycloKey1, block)).ToArray();
        R4Rc4.Apply(CycloKey0, header).CopyTo(patched, recordOffset);

        var cheats = R4EvolutionCheats.Load(patched).Games[0].Items.Cast<R4Cheat>().ToList();

        Assert.True(cheats[0].Enabled);
        Assert.False(cheats[1].Enabled);
    }

    // Returns each region of the file in plaintext, skipping the zlib payload whose bytes are implementation-defined.
    private static List<string> Decrypt(byte[] file, R4EvolutionVariant variant)
    {
        var (encStart, headerLength, key0, key1) = variant == R4EvolutionVariant.Edge
            ? (8, 0x28, Convert.FromHexString("08D7B92A4CC591A70E9B249BF7DFD744"),
                Convert.FromHexString("C14EB9F9B970FFADF1971A1CE558D93E"))
            : (0x20, 0x30, CycloKey0, CycloKey1);

        var regions = new List<string> { Convert.ToHexString(file[..encStart]) };
        var count = BinaryPrimitives.ReadInt32LittleEndian(R4Rc4.Apply(key0, file.AsSpan(encStart, headerLength)).AsSpan(0x10));
        var plain = R4Rc4.Apply(key0, file.AsSpan(encStart, headerLength + 16 * count));
        var index = plain.AsSpan(headerLength);

        // The index MD5 and record offsets depend on compressed sizes, so only compare the stable parts. The creator
        // tag at 0x24 is skipped too: r4cce writes "USAY" there, R4Everyone writes "R4EO".
        regions.Add(Convert.ToHexString(plain[..0x14]) + Convert.ToHexString(plain[0x28..headerLength]));
        for (var n = 0; n < count; n++)
        {
            var entry = index.Slice(n * 16, 16);
            regions.Add(Convert.ToHexString(entry[..12]));
            var offset = BinaryPrimitives.ReadInt32LittleEndian(entry[12..]);
            var gameHeader = R4Rc4.Apply(key0, file.AsSpan(offset, 0x20));
            var compressedSize = BinaryPrimitives.ReadInt32LittleEndian(gameHeader);
            var onOffOffset = BinaryPrimitives.ReadInt32LittleEndian(gameHeader.AsSpan(0x18));
            var onOffSize = BinaryPrimitives.ReadInt32LittleEndian(gameHeader.AsSpan(0x1C));
            Assert.Equal(offset + 0x20 + compressedSize, onOffOffset);

            var compressed = R4Rc4.Apply(key1, file.AsSpan(offset + 0x20, compressedSize));
            regions.Add("tree " + Convert.ToHexString(Inflate(compressed)));
            regions.Add("onoff " + Convert.ToHexString(R4Rc4.Apply(key1, file.AsSpan(onOffOffset, onOffSize))));
        }

        return regions;
    }

    private static (byte[] Tree, byte[] OnOff) ReadFirstGame(byte[] file)
    {
        var variant = file[0] == (byte)'E' && file[1] == (byte)'D' ? R4EvolutionVariant.Edge : R4EvolutionVariant.CycloDsEvolution;
        var regions = Decrypt(file, variant);
        return (Convert.FromHexString(regions[3]["tree ".Length..]), Convert.FromHexString(regions[4]["onoff ".Length..]));
    }

    private static byte[] Inflate(byte[] compressed)
    {
        using var zlib = new System.IO.Compression.ZLibStream(new MemoryStream(compressed),
            System.IO.Compression.CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return output.ToArray();
    }
}
