namespace R4Everyone.Binary4Everyone.Tests;

internal static partial class ReferenceFixtures
{
    public static List<byte[]> Words(params uint[] words) => words.Select(BitConverter.GetBytes).ToList();

    /// <summary>
    /// The database the reference implementation wrote the fixtures from.
    /// </summary>
    public static R4Database BuildDatabase()
    {
        var db = new R4Database { FileEncoding = R4Encoding.UTF8 };

        var mario = new R4Game("ASME") { GameTitle = "Super Mario 64 DS", GameChecksum = "2F00B549" };
        mario.Items.Add(new R4Cheat
            { Title = "99 Lives", Description = "loose note", Enabled = true, Code = Words(0x0209F49C, 0x63) });
        var movement = new R4Folder { Title = "Movement", Description = "Pick one", OneHot = true };
        movement.Items.Add(new R4Cheat
        {
            Title = "Moon Jump", Description = "Hold L", Enabled = true,
            Code = Words(0x94000130, 0xFDFF0000, 0x00000001, 0x00000002)
        });
        movement.Items.Add(new R4Cheat
            { Title = "Speed", Description = "", Enabled = false, Code = Words(0x12345678, 0x9ABCDEF0) });
        mario.Items.Add(movement);
        mario.Items.Add(new R4Cheat { Title = "Tail", Description = "", Enabled = false, Code = [] });

        var pokemon = new R4Game("ADAE") { GameTitle = "ポケモン ダイヤモンド", GameChecksum = "12345678" };
        pokemon.Items.Add(new R4Folder { Title = "Empty", Description = "" });
        pokemon.Items.Add(new R4Cheat
        {
            Title = "名前]<[犠ゾ", Description = "メモ >}{<マ±", Enabled = true, Code = Words(0xDEADBEEF, 0x00000000)
        });
        var items = new R4Folder { Title = "Items{x}", Description = "" };
        items.Items.Add(new R4Cheat { Title = "Master Ball", Description = "", Enabled = false, Code = Words(1, 2) });
        pokemon.Items.Add(items);

        db.Games.Add(mario);
        db.Games.Add(pokemon);
        db.MarkGameDirty(mario);
        db.MarkGameDirty(pokemon);
        return db;
    }

    /// <summary>
    /// <see cref="BuildDatabase"/> without its non-ASCII game, for round trips through the R4 writer, which only
    /// writes ASCII text.
    /// </summary>
    public static R4Database BuildAsciiDatabase()
    {
        var db = BuildDatabase();
        db.Games.RemoveAt(1);
        return db;
    }

    /// <summary>
    /// Compares the parts of two databases that CycloDS Evolution and EDGE files store.
    /// </summary>
    public static void AssertEquivalent(R4Database expected, R4Database actual)
    {
        Xunit.Assert.Equal(Describe(expected), Describe(actual));
    }

    private static string Describe(R4Database db)
    {
        db.MaterializeAllGames();
        var lines = new List<string>();
        foreach (var game in db.Games)
        {
            lines.Add($"game {game.GameId} {game.GameChecksum} {game.GameTitle}");
            Describe(game.Items, "  ", lines);
        }

        return string.Join('\n', lines);
    }

    private static void Describe(IEnumerable<IR4Item> items, string indent, List<string> lines)
    {
        foreach (var item in items)
        {
            switch (item)
            {
                case R4Folder folder:
                    lines.Add($"{indent}folder '{folder.Title}' note='{folder.Description}' onehot={folder.OneHot}");
                    Describe(folder.Items, indent + "  ", lines);
                    break;
                case R4Cheat cheat:
                    var words = string.Join(' ', cheat.Code.Select(b => BitConverter.ToUInt32(b).ToString("X8")));
                    lines.Add($"{indent}cheat '{cheat.Title}' note='{cheat.Description}' on={cheat.Enabled} [{words}]");
                    break;
            }
        }
    }
}
