using System.Buffers.Binary;
using System.Text;
using Xunit;

namespace R4Everyone.Binary4Everyone.Tests;

public class R4M3RealDbTests
{
    private static readonly Encoding Sjis = R4M3RealDb.GetEncoding(R4Encoding.SJIS);

    [Fact]
    public void Export_ExtendedSjis_MatchesReferenceImplementation()
    {
        var bytes = R4M3RealDb.Export(ReferenceFixtures.BuildDatabase());

        Assert.Equal(ReferenceFixtures.M3RealExtendedSjis, bytes);
    }

    [Fact]
    public void Export_Dht_MatchesReferenceImplementation()
    {
        var bytes = R4M3RealDb.Export(ReferenceFixtures.BuildDatabase(), new R4M3RealOptions { Mode = R4M3RealMode.Dht });

        Assert.Equal(ReferenceFixtures.M3RealDhtSjis, bytes);
    }

    [Fact]
    public void Export_NoteAfterName_MatchesReferenceImplementation()
    {
        var bytes = R4M3RealDb.Export(ReferenceFixtures.BuildDatabase(),
            new R4M3RealOptions { NotePosition = R4M3RealNotePosition.AfterName });

        Assert.Equal(ReferenceFixtures.M3RealNoteAfterSjis, bytes);
    }

    [Fact]
    public void Export_Utf8WithoutCharacterFix_MatchesReferenceImplementation()
    {
        var bytes = R4M3RealDb.Export(ReferenceFixtures.BuildDatabase(),
            new R4M3RealOptions { Encoding = R4Encoding.UTF8, CharacterFix = false });

        Assert.Equal(ReferenceFixtures.M3RealExtendedUtf8NoFix, bytes);
    }

    [Fact]
    public void Export_IndexEntriesPointAtEachGameSection()
    {
        var bytes = R4M3RealDb.Export(ReferenceFixtures.BuildDatabase());

        Assert.Equal(2, BinaryPrimitives.ReadUInt16LittleEndian(bytes));
        Assert.Equal("ASMEDHT2F00B549", Encoding.ASCII.GetString(bytes, 2, 15));
        Assert.Equal("ADAEDHT12345678", Encoding.ASCII.GetString(bytes, 2 + 23, 15));
        Assert.Equal("\r\n", Encoding.ASCII.GetString(bytes, 2 + 2 * 23, 2));

        var firstOffset = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(2 + 15));
        var firstLength = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(2 + 19));
        var secondOffset = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(2 + 23 + 15));
        var secondLength = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(2 + 23 + 19));
        Assert.Equal(2 + 2 * 23 + 2, firstOffset);
        Assert.Equal(firstOffset + firstLength, secondOffset);
        Assert.Equal(bytes.Length, secondOffset + secondLength);
        Assert.StartsWith(";Game ID: ASME 2F00B549", Sjis.GetString(bytes, firstOffset, firstLength));
    }

    [Fact]
    public void Export_PadsShortGameIdWithQuestionMarks()
    {
        var db = new R4Database();
        db.Games.Add(new R4Game("AB") { GameTitle = "G", GameChecksum = "00000001" });

        var bytes = R4M3RealDb.Export(db);

        Assert.Equal("AB??DHT00000001", Encoding.ASCII.GetString(bytes, 2, 15));
    }

    [Fact]
    public void Export_InvalidChecksum_Throws()
    {
        var db = new R4Database();
        db.Games.Add(new R4Game("ABCD") { GameChecksum = "nope" });

        Assert.Throws<FormatException>(() => R4M3RealDb.Export(db));
    }

    [Theory]
    [InlineData("A[B]C", true, "A<B>C")]
    [InlineData("ゾ", true, "ぞ")]
    [InlineData("A<B{C}D>E", false, "A[B[C]D]E")]
    [InlineData("マ", false, "ま")]
    public void FixName_ReplacesBracketsAndKnownCharacters(string input, bool isCodeName, string expected)
    {
        Assert.Equal(expected, R4M3RealDb.FixName(input, isCodeName, Sjis));
    }

    [Fact]
    public void FixName_BlanksDoubleByteCharactersWhoseTrailByteIsAClosingBracket()
    {
        // 犠 is 0x8B5D in Shift-JIS; its trail byte is ']'.
        Assert.Equal("名前  ", R4M3RealDb.FixName("名前犠", isCodeName: true, Sjis));
        // ポ (0x837C) and ン (0x8393) have harmless trail bytes.
        Assert.Equal("ポン", R4M3RealDb.FixName("ポン", isCodeName: false, Sjis));
    }

    [Fact]
    public void FixName_DoesNotMisreadTrailByteAsLeadByte()
    {
        // モ is 0x83 0x82; r4cce would treat 0x82 as a lead byte and blank the following '>'. It should become ']'.
        Assert.Equal("モ]", R4M3RealDb.FixName("モ>", isCodeName: false, Sjis));
    }

    [Fact]
    public void FixName_LeavesUtf8MultibyteCharactersAlone()
    {
        var utf8 = R4M3RealDb.GetEncoding(R4Encoding.UTF8);

        Assert.Equal("犠>", R4M3RealDb.FixName("犠]", isCodeName: true, utf8));
    }

    public static TheoryData<string, R4Encoding, R4M3RealNotePosition, R4M3RealMode, bool> Fixtures => new()
    {
        { nameof(ReferenceFixtures.M3RealExtendedSjis), R4Encoding.SJIS, R4M3RealNotePosition.BeforeName, R4M3RealMode.Extended, true },
        { nameof(ReferenceFixtures.M3RealDhtSjis), R4Encoding.SJIS, R4M3RealNotePosition.BeforeName, R4M3RealMode.Dht, true },
        { nameof(ReferenceFixtures.M3RealNoteAfterSjis), R4Encoding.SJIS, R4M3RealNotePosition.AfterName, R4M3RealMode.Extended, true },
        { nameof(ReferenceFixtures.M3RealExtendedUtf8NoFix), R4Encoding.UTF8, R4M3RealNotePosition.BeforeName, R4M3RealMode.Extended, false }
    };

    private static byte[] GetFixture(string name) =>
        (byte[])typeof(ReferenceFixtures).GetField(name, System.Reflection.BindingFlags.Static |
                                                         System.Reflection.BindingFlags.Public |
                                                         System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Inspect_DetectsSettingsTheReferenceFileWasWrittenWith(string fixture, R4Encoding encoding,
        R4M3RealNotePosition notePosition, R4M3RealMode mode, bool characterFix)
    {
        var info = R4M3RealDb.Inspect(GetFixture(fixture));

        Assert.Equal(new R4M3RealFileInfo(2, encoding, notePosition, mode, characterFix), info);
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void LoadThenExport_ReproducesReferenceFile(string fixture, R4Encoding encoding,
        R4M3RealNotePosition notePosition, R4M3RealMode mode, bool characterFix)
    {
        var original = GetFixture(fixture);

        var db = R4M3RealDb.Load(original);
        var options = R4M3RealDb.Inspect(original).ToWriteOptions();

        Assert.Equal(new R4M3RealOptions { Encoding = encoding, NotePosition = notePosition, Mode = mode, CharacterFix = characterFix }, options);
        Assert.Equal(original, R4M3RealDb.Export(db, options));
    }

    [Fact]
    public void Inspect_UsesGivenSettings()
    {
        var info = R4M3RealDb.Inspect(ReferenceFixtures.M3RealNoteAfterSjis,
            new R4M3RealReadOptions { Encoding = R4Encoding.GBK, NotePosition = R4M3RealNotePosition.BeforeName });

        Assert.Equal(R4Encoding.GBK, info.Encoding);
        Assert.Equal(R4M3RealNotePosition.BeforeName, info.NotePosition);
    }

    [Fact]
    public void Load_Utf8WithoutCharacterFix_RestoresEverythingTheFormatStores()
    {
        var expected = ReferenceFixtures.BuildDatabase();
        foreach (var item in expected.Games.SelectMany(g => g.Items).SelectMany(Flatten))
        {
            switch (item)
            {
                case R4Cheat cheat:
                    cheat.Enabled = false; // ON/OFF state is not stored
                    break;
                case R4Folder folder:
                    folder.OneHot = false; // Extended mode does not mark one-hot folders
                    break;
            }
        }

        var db = R4M3RealDb.Load(ReferenceFixtures.M3RealExtendedUtf8NoFix);

        ReferenceFixtures.AssertEquivalent(expected, db);
    }

    [Fact]
    public void Load_Dht_KeepsOneHotAndUsesGameIdAsTitle()
    {
        var db = R4M3RealDb.Load(ReferenceFixtures.M3RealDhtSjis);

        var mario = db.Games[0];
        Assert.Equal("ASME", mario.GameTitle);
        Assert.Equal("2F00B549", mario.GameChecksum);
        var movement = Assert.IsType<R4Folder>(mario.Items[1]);
        Assert.True(movement.OneHot);
        Assert.Equal("", movement.Description);
        Assert.Equal(["Moon Jump", "Speed"], movement.Items.Select(i => ((R4Cheat)i).Title));
        Assert.Equal(ReferenceFixtures.Words(0x94000130, 0xFDFF0000, 1, 2), ((R4Cheat)movement.Items[0]).Code);
        Assert.IsType<R4Cheat>(mario.Items[2]);

        var pokemon = db.Games[1];
        Assert.Equal(R4M3RealDb.FixName("名前]<[犠ゾ", isCodeName: true, Sjis), ((R4Cheat)pokemon.Items[1]).Title);
        Assert.Equal("Items[x]", ((R4Folder)pokemon.Items[2]).Title);
    }

    [Fact]
    public void Load_NoteAfterName_AttachesNotesToPrecedingItem()
    {
        var db = R4M3RealDb.Load(ReferenceFixtures.M3RealNoteAfterSjis);

        var mario = db.Games[0];
        Assert.Equal("loose note", ((R4Cheat)mario.Items[0]).Description);
        var movement = (R4Folder)mario.Items[1];
        Assert.Equal("Pick one", movement.Description);
        Assert.Equal("Hold L", ((R4Cheat)movement.Items[0]).Description);
        Assert.Equal("", ((R4Cheat)movement.Items[1]).Description);
        Assert.Equal(R4M3RealDb.FixName("メモ >}{<マ±", isCodeName: false, Sjis),
            ((R4Cheat)db.Games[1].Items[1]).Description);
    }

    [Fact]
    public void Load_WrongNotePosition_ShiftsNotes()
    {
        var db = R4M3RealDb.Load(ReferenceFixtures.M3RealNoteAfterSjis,
            new R4M3RealReadOptions { NotePosition = R4M3RealNotePosition.BeforeName });

        Assert.Equal("", ((R4Cheat)db.Games[0].Items[0]).Description);
    }

    [Fact]
    public void Load_ShortGameIdUsesCommentLine()
    {
        var db = new R4Database();
        db.Games.Add(new R4Game("AB") { GameTitle = "G", GameChecksum = "0000000A" });

        var loaded = R4M3RealDb.Load(R4M3RealDb.Export(db));

        Assert.Equal("AB", loaded.Games[0].GameId);
        Assert.Equal("0000000A", loaded.Games[0].GameChecksum);
    }

    [Fact]
    public void Load_EmptyDatabase()
    {
        var bytes = R4M3RealDb.Export(new R4Database());

        Assert.True(R4M3RealDb.TryDetect(bytes));
        Assert.Empty(R4M3RealDb.Load(bytes).Games);
    }

    [Theory]
    [InlineData(2 + 4)] // DHT tag
    [InlineData(2 + 9)] // CRC hex digit
    [InlineData(2 + 15)] // section offset
    public void TryDetect_RejectsCorruptIndex(int position)
    {
        var file = ReferenceFixtures.M3RealExtendedSjis.ToArray();
        file[position] ^= 0x40;

        Assert.False(R4M3RealDb.TryDetect(file));
        Assert.Throws<InvalidDataException>(() => R4M3RealDb.Load(file));
    }

    [Fact]
    public void Load_UnrecognizedLine_Throws()
    {
        var text = ";Game ID: ABCD 00000000\r\nnonsense\r\n\0"u8.ToArray();
        byte[] file = [1, 0, .. "ABCDDHT00000000"u8, 0, 0, 0, 0, 0, 0, 0, 0, .. "\r\n"u8, .. text];
        BitConverter.GetBytes(2 + 23 + 2).CopyTo(file, 2 + 15);
        BitConverter.GetBytes(text.Length).CopyTo(file, 2 + 19);

        var ex = Assert.Throws<InvalidDataException>(() => R4M3RealDb.Load(file));
        Assert.Contains("nonsense", ex.Message);
    }

    private static IEnumerable<IR4Item> Flatten(IR4Item item) =>
        item is R4Folder folder ? [folder, .. folder.Items.SelectMany(Flatten)] : [item];
}
