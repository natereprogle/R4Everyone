using R4Everyone.Binary4Everyone;

namespace R4Everyone.Web.State;

public partial class EditorState
{
    public R4Game AddGame()
    {
        var game = new R4Game(string.Empty)
        {
            GameTitle = "New Game",
            GameEnabled = true,
            GameChecksum = string.Empty,
            MasterCodes = new uint[MasterCodeCount]
        };

        EnsureDatabase().Games.Add(game);
        Select(SelectionInfo.ForGame(game));
        MarkDirty(game);
        return game;
    }

    public void RemoveGame(R4Game game)
    {
        if (Database == null || !Database.Games.Remove(game))
        {
            return;
        }

        if (ReferenceEquals(SelectedGame, game))
        {
            ClearSelection();
        }

        MarkDirty(null);
    }

    /// <summary>
    /// Adds a folder at the top level of the selected game.
    /// </summary>
    public void AddFolder()
    {
        if (SelectedGame == null)
        {
            return;
        }

        var folder = new R4Folder
        {
            Title = "New Folder",
            Description = string.Empty,
            OneHot = false
        };

        SelectedGame.Items.Add(folder);
        Select(SelectionInfo.ForFolder(SelectedGame, folder));
        MarkDirty(SelectedGame);
    }

    /// <summary>
    /// Adds a cheat to the selected folder, or to the folder containing the selected cheat, or to the top level of
    /// the selected game.
    /// </summary>
    public void AddCheat()
    {
        if (SelectedGame == null)
        {
            return;
        }

        var cheat = new R4Cheat
        {
            Title = "New Cheat",
            Description = string.Empty,
            Enabled = false
        };

        if (SelectedFolder != null)
        {
            SelectedFolder.Items.Add(cheat);
        }
        else
        {
            SelectedGame.Items.Add(cheat);
        }

        Select(SelectionInfo.ForCheat(SelectedGame, SelectedFolder, cheat));
        MarkDirty(SelectedGame);
    }

    /// <summary>
    /// Removes the selected folder or cheat and selects its parent.
    /// </summary>
    public void RemoveSelectedItem()
    {
        if (SelectedGame == null)
        {
            return;
        }

        var game = SelectedGame;
        switch (SelectionKind)
        {
            case SelectionKind.Folder when SelectedFolder != null:
                game.Items.Remove(SelectedFolder);
                Select(SelectionInfo.ForGame(game));
                break;
            case SelectionKind.Cheat when SelectedCheat != null && SelectedFolder != null:
                SelectedFolder.Items.Remove(SelectedCheat);
                Select(SelectionInfo.ForFolder(game, SelectedFolder));
                break;
            case SelectionKind.Cheat when SelectedCheat != null:
                game.Items.Remove(SelectedCheat);
                Select(SelectionInfo.ForGame(game));
                break;
            default:
                return;
        }

        MarkDirty(game);
    }

    public void UpdateGameId(R4Game game, string value)
    {
        game.GameId = value;
        MarkDirty(game);
    }

    public void UpdateGameTitle(R4Game game, string value)
    {
        game.GameTitle = value;
        MarkDirty(game);
    }

    public void UpdateGameEnabled(R4Game game, bool value)
    {
        game.GameEnabled = value;
        MarkDirty(game);
    }

    public void UpdateGameChecksum(R4Game game, string value)
    {
        game.GameChecksum = value;
        MarkDirty(game);
    }

    public void UpdateFolderTitle(string value)
    {
        if (SelectedFolder == null)
        {
            return;
        }

        SelectedFolder.Title = value;
        MarkDirty(SelectedGame);
    }

    public void UpdateFolderDescription(string value)
    {
        if (SelectedFolder == null)
        {
            return;
        }

        SelectedFolder.Description = value;
        MarkDirty(SelectedGame);
    }

    /// <summary>
    /// Sets the folder's One Hot flag. Turning it on keeps only the first enabled cheat enabled.
    /// </summary>
    public void UpdateFolderOneHot(bool value)
    {
        if (SelectedFolder == null)
        {
            return;
        }

        SelectedFolder.OneHot = value;
        if (value)
        {
            var keptOne = false;
            foreach (var cheat in SelectedFolder.Items.OfType<R4Cheat>().Where(cheat => cheat.Enabled))
            {
                cheat.Enabled = !keptOne;
                keptOne = true;
            }
        }

        MarkDirty(SelectedGame);
    }

    public void UpdateCheatTitle(string value)
    {
        if (SelectedCheat == null)
        {
            return;
        }

        SelectedCheat.Title = value;
        MarkDirty(SelectedGame);
    }

    public void UpdateCheatDescription(string value)
    {
        if (SelectedCheat == null)
        {
            return;
        }

        SelectedCheat.Description = value;
        MarkDirty(SelectedGame);
    }

    /// <summary>
    /// Enables or disables the selected cheat. In a One Hot folder, enabling a cheat disables its siblings.
    /// </summary>
    public void UpdateCheatEnabled(bool value)
    {
        if (SelectedCheat == null)
        {
            return;
        }

        SelectedCheat.Enabled = value;
        if (value && SelectedFolder is { OneHot: true })
        {
            foreach (var sibling in SelectedFolder.Items.OfType<R4Cheat>())
            {
                sibling.Enabled = ReferenceEquals(sibling, SelectedCheat);
            }
        }

        MarkDirty(SelectedGame);
    }
}
