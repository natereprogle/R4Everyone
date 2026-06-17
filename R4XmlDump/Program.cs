using System.Text;
using System.Xml.Linq;
using R4Everyone.Binary4Everyone;

Encoding DetectEncoding(byte[] data)
{
    if (data.Length < 0x50) return Encoding.UTF8;

    var flag = Encoding.ASCII.GetString(data, 0x4C, 4);

    return flag switch
    {
        "UsAY" => Encoding.UTF8,
        "uSAY" => Encoding.GetEncoding("shift_jis"),
        "dSAY" => Encoding.GetEncoding("GBK"),
        "fSAY" => Encoding.GetEncoding("big5"),
        _ => Encoding.UTF8
    };
}

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var raw = File.ReadAllBytes("usrcheat.dat");
R4Binary.CurrentEncoding = DetectEncoding(raw);

await using var db = await R4CheatDat.LoadAsync("usrcheat.dat");
db.MaterializeAllGames();

var root = new XElement("codelist",
    new XElement("name", "R4 Converted")
);

foreach (var game in db.Games)
{
    db.EnsureGameMaterialized(game);

    root.Add(new XElement("game",
        new XElement("name", game.GameTitle ?? ""),
        new XElement("gameid", $"{game.GameId ?? ""} {game.GameChecksum ?? ""}"),
        DumpItems(game.Items.Cast<object>())
    ));
}

var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
doc.Save("r4_dump.xml");
Console.WriteLine("done: r4_dump.xml");

IEnumerable<XElement> DumpItems(IEnumerable<object> items)
{
    foreach (var item in items)
    {
        switch (item)
        {
            case R4Folder folder:
                yield return new XElement("folder",
                    new XElement("name",
                        string.IsNullOrWhiteSpace(folder.Title) ? "Default" : folder.Title),
                    DumpItems(folder.Items.Cast<object>())
                );
                break;

            case R4Cheat cheat:
                var lines = new List<string>();

                for (int i = 0; i < cheat.Code.Count; i += 2)
                {
                    string a = FormatWord(cheat.Code[i]);
                    string b = (i + 1 < cheat.Code.Count)
                        ? FormatWord(cheat.Code[i + 1])
                        : "00000000";

                    lines.Add($"{a} {b}");
                }

                var cheatElement = new XElement("cheat",
                    new XElement("name", cheat.Title ?? ""),
                    new XElement("codes", string.Join("\n", lines))
                );

                if (!string.IsNullOrWhiteSpace(cheat.Description))
                {
                    cheatElement.Add(new XElement("note", cheat.Description));
                }

                yield return cheatElement;
                break;
        }
    }
}

string FormatWord(byte[] raw)
{
    if (raw == null || raw.Length != 4)
        return "00000000";

    uint value = BitConverter.ToUInt32(raw, 0);
    return value.ToString("X8");
}