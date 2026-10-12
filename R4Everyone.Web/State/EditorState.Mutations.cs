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

        Change(null, () =>
        {
            EnsureDatabase().Games.Add(game);
            Database!.MarkGameDirty(game);
            Select(SelectionInfo.ForGame(game));
        });
        return game;
    }

    public void RemoveGame(R4Game game)
    {
        if (Database?.Games.Contains(game) != true)
        {
            return;
        }

        Change(null, () =>
        {
            Database.Games.Remove(game);
            if (ReferenceEquals(SelectedGame, game))
            {
                ClearSelection();
            }
        });
    }

    /// <summary>
    /// Adds a folder at the top level of the selected game.
    /// </summary>
    public void AddFolder()
    {
        if (SelectedGame is not { } game)
        {
            return;
        }

        var folder = new R4Folder
        {
            Title = "New Folder",
            Description = string.Empty,
            OneHot = false
        };

        Change(game, () =>
        {
            game.Items.Add(folder);
            Select(SelectionInfo.ForFolder(game, folder));
        });
    }

    /// <summary>
    /// Adds a cheat to the selected folder, or to the folder containing the selected cheat, or to the top level of
    /// the selected game.
    /// </summary>
    public void AddCheat()
    {
        if (SelectedGame is not { } game)
        {
            return;
        }

        var cheat = new R4Cheat
        {
            Title = "New Cheat",
            Description = string.Empty,
            Enabled = false
        };

        var folder = SelectedFolder;
        Change(game, () =>
        {
            (folder?.Items ?? game.Items).Add(cheat);
            Select(SelectionInfo.ForCheat(game, folder, cheat));
        });
    }

    /// <summary>
    /// Removes the selected folder or cheat and selects its parent.
    /// </summary>
    public void RemoveSelectedItem()
    {
        if (SelectedGame is not { } game || SelectedItem is not { } item ||
            FindParentItems(game, item) is not { } siblings)
        {
            return;
        }

        var parentFolder = SelectionKind == SelectionKind.Cheat ? SelectedFolder : null;
        Change(game, () =>
        {
            siblings.Remove(item);
            Select(parentFolder != null ? SelectionInfo.ForFolder(game, parentFolder) : SelectionInfo.ForGame(game));
        });
    }

    public void UpdateGameId(R4Game game, string value) =>
        Change(game, () => game.GameId = value, game, nameof(R4Game.GameId));

    public void UpdateGameTitle(R4Game game, string value) =>
        Change(game, () => game.GameTitle = value, game, nameof(R4Game.GameTitle));

    public void UpdateGameEnabled(R4Game game, bool value) =>
        Change(game, () => game.GameEnabled = value);

    public void UpdateGameChecksum(R4Game game, string value) =>
        Change(game, () => game.GameChecksum = value, game, nameof(R4Game.GameChecksum));

    public void UpdateFolderTitle(string value)
    {
        if (SelectedFolder is { } folder)
        {
            Change(SelectedGame, () => folder.Title = value, folder, nameof(R4Folder.Title));
        }
    }

    public void UpdateFolderDescription(string value)
    {
        if (SelectedFolder is { } folder)
        {
            Change(SelectedGame, () => folder.Description = value, folder, nameof(R4Folder.Description));
        }
    }

    /// <summary>
    /// Sets the folder's One Hot flag. Turning it on keeps only the first enabled cheat enabled.
    /// </summary>
    public void UpdateFolderOneHot(bool value)
    {
        if (SelectedFolder is not { } folder)
        {
            return;
        }

        Change(SelectedGame, () =>
        {
            folder.OneHot = value;
            if (value)
            {
                var keptOne = false;
                foreach (var cheat in folder.Items.OfType<R4Cheat>().Where(cheat => cheat.Enabled))
                {
                    cheat.Enabled = !keptOne;
                    keptOne = true;
                }
            }
        });
    }

    public void UpdateCheatTitle(string value)
    {
        if (SelectedCheat is { } cheat)
        {
            Change(SelectedGame, () => cheat.Title = value, cheat, nameof(R4Cheat.Title));
        }
    }

    public void UpdateCheatDescription(string value)
    {
        if (SelectedCheat is { } cheat)
        {
            Change(SelectedGame, () => cheat.Description = value, cheat, nameof(R4Cheat.Description));
        }
    }

    /// <summary>
    /// Enables or disables the selected cheat. In a One Hot folder, enabling a cheat disables its siblings.
    /// </summary>
    public void UpdateCheatEnabled(bool value)
    {
        if (SelectedCheat is not { } cheat)
        {
            return;
        }

        var folder = SelectedFolder;
        Change(SelectedGame, () =>
        {
            cheat.Enabled = value;
            if (value && folder is { OneHot: true })
            {
                foreach (var sibling in folder.Items.OfType<R4Cheat>())
                {
                    sibling.Enabled = ReferenceEquals(sibling, cheat);
                }
            }
        });
    }
}
