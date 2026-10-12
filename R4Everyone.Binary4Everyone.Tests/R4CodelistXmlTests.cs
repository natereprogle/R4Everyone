using Xunit;

namespace R4Everyone.Binary4Everyone.Tests;

public class R4CodelistXmlTests
{
    private static List<byte[]> Words(params uint[] words) => words.Select(BitConverter.GetBytes).ToList();

    private static R4Database BuildDatabase()
    {
        var db = new R4Database { Title = "My Cheats", FileEncoding = R4Encoding.UTF8 };
        var game = new R4Game("ASME")
        {
            GameTitle = "Super Mario 64 DS",
            GameChecksum = "2F00B549",
            GameEnabled = false,
            MasterCodes = [1, 2, 3, 4, 5, 6, 7, 0xDEADBEEF]
        };

        var folder = new R4Folder { Title = "Movement", Description = "Pick one", OneHot = true };
        folder.Items.Add(new R4Cheat { Title = "Moon Jump", Description = "Hold L", Enabled = true, Code = Words(0x94000130, 0xFDFF0000) });
        game.Items.Add(folder);
        game.Items.Add(new R4Cheat { Title = "99 Lives", Description = "", Enabled = false, Code = Words(0x0209F49C, 0x00000063) });
        db.Games.Add(game);
        db.MarkGameDirty(game);
        return db;
    }

    [Fact]
    public void ExportThenImport_RoundTripsEveryField()
    {
        var imported = R4CodelistXml.Import(R4CodelistXml.Export(BuildDatabase()));

        Assert.Equal("My Cheats", imported.Title);
        var game = Assert.Single(imported.Games);
        Assert.Equal("ASME", game.GameId);
        Assert.Equal("2F00B549", game.GameChecksum);
        Assert.Equal("Super Mario 64 DS", game.GameTitle);
        Assert.False(game.GameEnabled);
        Assert.Equal(new uint[] { 1, 2, 3, 4, 5, 6, 7, 0xDEADBEEF }, game.MasterCodes);

        var folder = Assert.IsType<R4Folder>(game.Items[0]);
        Assert.Equal("Movement", folder.Title);
        Assert.Equal("Pick one", folder.Description);
        Assert.True(folder.OneHot);

        var moonJump = Assert.IsType<R4Cheat>(Assert.Single(folder.Items));
        Assert.Equal("Moon Jump", moonJump.Title);
        Assert.Equal("Hold L", moonJump.Description);
        Assert.True(moonJump.Enabled);
        Assert.Equal(Words(0x94000130, 0xFDFF0000), moonJump.Code);

        var lives = Assert.IsType<R4Cheat>(game.Items[1]);
        Assert.False(lives.Enabled);
        Assert.Equal(Words(0x0209F49C, 0x00000063), lives.Code);
    }

    [Fact]
    public void Export_WritesCodesAsBigEndianWordPairs()
    {
        var xml = R4CodelistXml.Export(BuildDatabase());

        Assert.Contains("<gameid>ASME 2F00B549</gameid>", xml);
        Assert.Contains("<codes>94000130 FDFF0000</codes>", xml);
    }

    [Fact]
    public void Import_PlainCodelistWithoutExtras_UsesDefaults()
    {
        // The shape R4XmlDump produces.
        const string xml = """
            <codelist>
              <name>R4 Converted</name>
              <game>
                <name>Pokemon Diamond</name>
                <gameid>ADAE 12345678</gameid>
                <folder>
                  <name>Items</name>
                  <cheat><name>Master Ball</name><codes>12345678 9ABCDEF0
            0000000A 0000000B</codes></cheat>
                </folder>
              </game>
            </codelist>
            """;

        var db = R4CodelistXml.Import(xml);

        var game = Assert.Single(db.Games);
        Assert.True(game.GameEnabled);
        Assert.Equal(new R4Game("x").MasterCodes, game.MasterCodes);
        var folder = Assert.IsType<R4Folder>(Assert.Single(game.Items));
        Assert.False(folder.OneHot);
        var cheat = Assert.IsType<R4Cheat>(Assert.Single(folder.Items));
        Assert.False(cheat.Enabled);
        Assert.Equal(Words(0x12345678, 0x9ABCDEF0, 0x0A, 0x0B), cheat.Code);
        Assert.Equal(R4Encoding.UTF8, db.FileEncoding);
    }

    [Fact]
    public void Import_DuplicateGames_AreMergedAndDuplicateCheatsSkipped()
    {
        const string xml = """
            <codelist>
              <game><name>A</name><gameid>ASME 2F00B549</gameid>
                <folder><name>Movement</name><cheat><name>Jump</name><codes>00000001 00000002</codes></cheat></folder>
              </game>
              <game><name>A again</name><gameid>asme 2f00b549</gameid>
                <folder><name>movement</name>
                  <cheat><name>Jump</name><codes>00000001 00000002</codes></cheat>
                  <cheat><name>Run</name><codes>00000003 00000004</codes></cheat>
                </folder>
                <cheat><name>Lives</name><codes>00000005 00000006</codes></cheat>
              </game>
            </codelist>
            """;

        var db = R4CodelistXml.Import(xml);

        var game = Assert.Single(db.Games);
        var folder = Assert.IsType<R4Folder>(game.Items[0]);
        Assert.Equal(["Jump", "Run"], folder.Items.Select(i => i.Title));
        Assert.Equal("Lives", Assert.IsType<R4Cheat>(game.Items[1]).Title);
    }

    [Fact]
    public void Import_OddNumberOfCodeWords_PadsFinalPair()
    {
        const string xml = "<codelist><game><name>G</name><gameid>ASME 2F00B549</gameid><cheat><name>C</name><codes>00000001</codes></cheat></game></codelist>";

        var cheat = Assert.IsType<R4Cheat>(Assert.Single(R4CodelistXml.Import(xml).Games[0].Items));

        Assert.Equal(Words(1, 0), cheat.Code);
    }

    [Theory]
    [InlineData("not xml at all")]
    [InlineData("<cheats></cheats>")]
    [InlineData("<codelist><game><name>G</name><cheat><name>C</name><codes>XYZ</codes></cheat></game></codelist>")]
    [InlineData("<codelist><game><name>G</name><mastercodes>00000000</mastercodes></game></codelist>")]
    [InlineData("<codelist><game><name>G</name><enabled>maybe</enabled></game></codelist>")]
    public void Import_InvalidInput_ThrowsFormatException(string xml)
    {
        Assert.Throws<FormatException>(() => R4CodelistXml.Import(xml));
    }

    [Fact]
    public void Import_LongOrNonAsciiTitle_KeepsDefaultTitle()
    {
        var defaultTitle = new R4Database().Title;

        Assert.Equal(defaultTitle, R4CodelistXml.Import($"<codelist><name>{new string('x', 61)}</name></codelist>").Title);
        Assert.Equal(defaultTitle, R4CodelistXml.Import("<codelist><name>チート</name></codelist>").Title);
    }

    [Fact]
    public async Task ImportedDatabase_SavesAndReloadsAsDat()
    {
        var imported = R4CodelistXml.Import(R4CodelistXml.Export(BuildDatabase()));

        using var stream = new MemoryStream();
        await R4CheatDat.SaveAsync(imported, stream);
        stream.Position = 0;
        var reloaded = await R4CheatDat.LoadAsync(stream);
        reloaded.MaterializeAllGames();

        var game = Assert.Single(reloaded.Games);
        Assert.Equal("ASME", game.GameId);
        Assert.Equal(2, game.Items.Count);
        Assert.Equal(R4CodelistXml.Export(imported), R4CodelistXml.Export(reloaded));
    }
}
