using R4Everyone.Binary4Everyone;

namespace R4Everyone.Web.State;

public partial class EditorState
{
    public void AddGame()
    {
        EnsureDatabase();
        var game = new R4Game(string.Empty)
        {
            GameTitle = "New Game",
            GameEnabled = true,
            GameChecksum = string.Empty,
            MasterCodes = new uint[8]
        };

        Database!.Games.Add(game);
        Select(SelectionInfo.ForGame(game));
        MarkDirty();
    }

    public void AddFolder()
    {
        if (SelectedGame == null || SelectionKind != SelectionKind.Game)
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
        MarkDirty();
    }

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

        if (SelectionKind == SelectionKind.Folder && SelectedFolder != null)
        {
            SelectedFolder.Items.Add(cheat);
            Select(SelectionInfo.ForCheat(SelectedGame, SelectedFolder, cheat));
        }
        else if (SelectionKind == SelectionKind.Game)
        {
            SelectedGame.Items.Add(cheat);
            Select(SelectionInfo.ForCheat(SelectedGame, null, cheat));
        }
        else
        {
            return;
        }

        MarkDirty();
    }

    public void RemoveSelected()
    {
        if (SelectionKind == SelectionKind.None)
        {
            return;
        }

        if (SelectionKind == SelectionKind.Game && SelectedGame != null)
        {
            Database?.Games.Remove(SelectedGame);
            ClearSelection();
            MarkDirty();
            return;
        }

        if (SelectedGame == null)
        {
            return;
        }

        if (SelectionKind == SelectionKind.Folder && SelectedFolder != null)
        {
            SelectedGame.Items.Remove(SelectedFolder);
            Select(SelectionInfo.ForGame(SelectedGame));
            MarkDirty();
            return;
        }

        if (SelectionKind == SelectionKind.Cheat && SelectedCheat != null)
        {
            if (SelectedFolder != null)
            {
                SelectedFolder.Items.Remove(SelectedCheat);
                Select(SelectionInfo.ForFolder(SelectedGame, SelectedFolder));
            }
            else
            {
                SelectedGame.Items.Remove(SelectedCheat);
                Select(SelectionInfo.ForGame(SelectedGame));
            }

            MarkDirty();
        }
    }

    public void UpdateGameId(string value)
    {
        if (SelectedGame == null)
        {
            return;
        }

        SelectedGame.GameId = value;
        MarkDirty();
    }

    public void UpdateGameTitle(string value)
    {
        if (SelectedGame == null)
        {
            return;
        }

        SelectedGame.GameTitle = value;
        MarkDirty();
    }

    public void UpdateGameEnabled(bool value)
    {
        if (SelectedGame == null)
        {
            return;
        }

        SelectedGame.GameEnabled = value;
        MarkDirty();
    }

    public void UpdateGameChecksum(string value)
    {
        if (SelectedGame == null)
        {
            return;
        }

        SelectedGame.GameChecksum = value;
        MarkDirty();
    }

    public string[] GetMasterCodeText(R4Game game)
    {
        EnsureMasterCodes(game);
        EnsureMasterCodeBuffer(game);
        return _masterCodeText[game];
    }

    public void UpdateMasterCode(R4Game game, int index, string value)
    {
        EnsureMasterCodes(game);
        EnsureMasterCodeBuffer(game);
        var buffer = _masterCodeText[game];

        if (index < 0 || index >= buffer.Length)
        {
            return;
        }

        buffer[index] = value;

        if (TryParseHex8ToUInt(value, out var parsed))
        {
            game.MasterCodes[index] = parsed;
        }

        MarkDirty();
    }

    public void UpdateFolderTitle(string value)
    {
        if (SelectedFolder == null)
        {
            return;
        }

        SelectedFolder.Title = value;
        MarkDirty();
    }

    public void UpdateFolderDescription(string value)
    {
        if (SelectedFolder == null)
        {
            return;
        }

        SelectedFolder.Description = value;
        MarkDirty();
    }

    public void UpdateFolderOneHot(bool value)
    {
        if (SelectedFolder == null)
        {
            return;
        }

        SelectedFolder.OneHot = value;
        MarkDirty();
    }

    public void UpdateCheatTitle(string value)
    {
        if (SelectedCheat == null)
        {
            return;
        }

        SelectedCheat.Title = value;
        MarkDirty();
    }

    public void UpdateCheatDescription(string value)
    {
        if (SelectedCheat == null)
        {
            return;
        }

        SelectedCheat.Description = value;
        MarkDirty();
    }

    public void UpdateCheatEnabled(bool value)
    {
        if (SelectedCheat == null)
        {
            return;
        }

        SelectedCheat.Enabled = value;

        if (value && SelectedFolder is { OneHot: true })
        {
            foreach (var item in SelectedFolder.Items.OfType<R4Cheat>())
            {
                if (!ReferenceEquals(item, SelectedCheat))
                {
                    item.Enabled = false;
                }
            }
        }

        MarkDirty();
    }

    public List<string> GetCheatCodeBlocks(R4Cheat cheat)
    {
        EnsureCheatCodeBuffer(cheat);
        return _cheatCodeText[cheat];
    }

    public void UpdateCheatBlock(R4Cheat cheat, int index, string value)
    {
        EnsureCheatCodeBuffer(cheat);
        var blocks = _cheatCodeText[cheat];

        if (index < 0 || index >= blocks.Count)
        {
            return;
        }

        blocks[index] = value;
        SyncCheatCodes(cheat, blocks);
        MarkDirty();
    }

    public void AddCheatRow(R4Cheat cheat)
    {
        EnsureCheatCodeBuffer(cheat);
        var blocks = _cheatCodeText[cheat];
        blocks.Add(string.Empty);
        blocks.Add(string.Empty);
        MarkDirty();
    }

    public void RemoveCheatRow(R4Cheat cheat, int rowIndex)
    {
        EnsureCheatCodeBuffer(cheat);
        var blocks = _cheatCodeText[cheat];
        var start = rowIndex * 2;

        if (start < 0 || start >= blocks.Count)
        {
            return;
        }

        var removeCount = Math.Min(2, blocks.Count - start);
        blocks.RemoveRange(start, removeCount);
        SyncCheatCodes(cheat, blocks);
        MarkDirty();
    }
}
