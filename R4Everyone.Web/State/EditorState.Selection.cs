using R4Everyone.Binary4Everyone;

namespace R4Everyone.Web.State;

public partial class EditorState
{
    public void ToggleGameExpand(R4Game game)
    {
        ToggleExpanded(game);
    }

    public void ToggleFolderExpand(R4Folder folder)
    {
        ToggleExpanded(folder);
    }

    public bool IsExpanded(R4Game game) => GetExpanded(game);

    public bool IsExpanded(R4Folder folder) => GetExpanded(folder);

    public void Select(SelectionInfo? selection)
    {
        if (selection == null)
        {
            ClearSelection();
            NotifyStateChanged();
            return;
        }

        SelectionKind = selection.Kind;
        SelectedGame = selection.Game;
        SelectedFolder = selection.Folder;
        SelectedCheat = selection.Cheat;

        if (SelectedGame != null)
        {
            Database?.EnsureGameMaterialized(SelectedGame);
            EnsureMasterCodes(SelectedGame);
            EnsureMasterCodeBuffer(SelectedGame);
        }

        if (SelectedCheat != null)
        {
            EnsureCheatCodeBuffer(SelectedCheat);
        }

        NotifyStateChanged();
    }

    private void ClearSelection()
    {
        SelectionKind = SelectionKind.None;
        SelectedGame = null;
        SelectedFolder = null;
        SelectedCheat = null;
    }

    public void Deselect()
    {
        ClearSelection();
        NotifyStateChanged();
    }

    private bool GetExpanded(object node)
    {
        return !_expandedState.TryGetValue(node, out var expanded) || expanded;
    }

    private void ToggleExpanded(object node)
    {
        var next = !GetExpanded(node);
        _expandedState[node] = next;
        NotifyStateChanged();
    }
}
