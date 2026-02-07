using R4Everyone.Binary4Everyone;

namespace R4Everyone.Web.State;

public sealed class SelectionInfo
{
    private SelectionInfo(SelectionKind kind, R4Game? game, R4Folder? folder, R4Cheat? cheat)
    {
        Kind = kind;
        Game = game;
        Folder = folder;
        Cheat = cheat;
    }

    public SelectionKind Kind { get; }
    public R4Game? Game { get; }
    public R4Folder? Folder { get; }
    public R4Cheat? Cheat { get; }

    public static SelectionInfo ForGame(R4Game game) => new(SelectionKind.Game, game, null, null);
    public static SelectionInfo ForFolder(R4Game game, R4Folder folder) => new(SelectionKind.Folder, game, folder, null);
    public static SelectionInfo ForCheat(R4Game game, R4Folder? folder, R4Cheat cheat) => new(SelectionKind.Cheat, game, folder, cheat);
}
