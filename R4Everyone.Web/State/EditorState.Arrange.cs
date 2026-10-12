using R4Everyone.Binary4Everyone;

namespace R4Everyone.Web.State;

/// <summary>
/// A folder a cheat can be moved into, or the top level of the game when <see cref="Folder"/> is null.
/// </summary>
public sealed record MoveTarget(R4Folder? Folder, string Label);

/// <summary>
/// Where a dragged item lands relative to the item it is dropped on.
/// </summary>
public enum DropPosition
{
    Before,
    After,

    /// <summary>
    /// At the end of the target folder.
    /// </summary>
    Inside
}

public enum MoveResult
{
    NotMoved,
    Moved,

    /// <summary>
    /// The cheat was moved and disabled, because it was enabled and its new One Hot folder already had an enabled
    /// cheat.
    /// </summary>
    MovedAndDisabled
}

/// <summary>
/// Reordering, moving, and sorting games, folders, and cheats.
/// </summary>
public partial class EditorState
{
    public const string TopLevelLabel = "Top level";

    private readonly Dictionary<object, int> _nodeIds = new(ReferenceEqualityComparer<object>.Instance);
    private readonly Dictionary<int, object> _nodesById = [];
    private int _lastNodeId;

    /// <summary>
    /// The selected folder or cheat, or null when a game or nothing is selected.
    /// </summary>
    public IR4Item? SelectedItem => SelectionKind switch
    {
        SelectionKind.Cheat => SelectedCheat,
        SelectionKind.Folder => SelectedFolder,
        _ => null
    };

    /// <summary>
    /// A stable number identifying a game, folder, or cheat, used to refer to it from drag-and-drop code in the
    /// browser. Look it up again with <see cref="FindNode"/>.
    /// </summary>
    public int GetNodeId(object node)
    {
        if (!_nodeIds.TryGetValue(node, out var id))
        {
            id = ++_lastNodeId;
            _nodeIds[node] = id;
            _nodesById[id] = node;
        }

        return id;
    }

    public object? FindNode(int id) => _nodesById.GetValueOrDefault(id);

    /// <summary>
    /// Moves a folder or cheat next to, or into, another item of the same game.
    /// </summary>
    /// <remarks>
    /// Cheats can move anywhere, including into a folder (<see cref="DropPosition.Inside"/>). Folders only reorder
    /// among their siblings, so moving never creates new levels of nesting.
    /// </remarks>
    public MoveResult MoveItem(R4Game game, IR4Item item, IR4Item target, DropPosition position)
    {
        if (ReferenceEquals(item, target) ||
            FindParentItems(game, item) is not { } source ||
            FindParentItems(game, target) is not { } targetParent)
        {
            return MoveResult.NotMoved;
        }

        List<IR4Item> destination;
        R4Folder? destinationFolder;
        if (position == DropPosition.Inside)
        {
            if (item is not R4Cheat || target is not R4Folder folder)
            {
                return MoveResult.NotMoved;
            }

            destination = folder.Items;
            destinationFolder = folder;
        }
        else
        {
            if (item is R4Folder && !ReferenceEquals(source, targetParent))
            {
                return MoveResult.NotMoved;
            }

            destination = targetParent;
            destinationFolder = FindFolderWithItems(game.Items, targetParent);
        }

        if (IsNoOpMove(source, destination, item, target, position))
        {
            return MoveResult.NotMoved;
        }

        var disable = item is R4Cheat { Enabled: true } &&
                      !ReferenceEquals(source, destination) &&
                      destinationFolder is { OneHot: true } &&
                      destination.OfType<R4Cheat>().Any(other => other.Enabled);

        Change(game, () =>
        {
            source.Remove(item);
            if (position == DropPosition.Inside)
            {
                destination.Add(item);
            }
            else
            {
                var index = destination.IndexOf(target);
                destination.Insert(position == DropPosition.After ? index + 1 : index, item);
            }

            if (disable)
            {
                ((R4Cheat)item).Enabled = false;
            }

            // Keep a moved selection selected, with its new parent folder.
            if (ReferenceEquals(SelectedItem, item))
            {
                Select(item is R4Cheat cheat
                    ? SelectionInfo.ForCheat(game, destinationFolder, cheat)
                    : SelectionInfo.ForFolder(game, (R4Folder)item));
            }
        });

        return disable ? MoveResult.MovedAndDisabled : MoveResult.Moved;
    }

    /// <summary>
    /// Moves a folder or cheat by <paramref name="offset"/> places among its siblings, for keyboard reordering.
    /// </summary>
    public MoveResult MoveItemBy(R4Game game, IR4Item item, int offset)
    {
        if (FindParentItems(game, item) is not { } siblings)
        {
            return MoveResult.NotMoved;
        }

        var index = siblings.IndexOf(item) + offset;
        if (offset == 0 || index < 0 || index >= siblings.Count)
        {
            return MoveResult.NotMoved;
        }

        return MoveItem(game, item, siblings[index], offset < 0 ? DropPosition.Before : DropPosition.After);
    }

    /// <summary>
    /// Moves a game before or after another game.
    /// </summary>
    public bool MoveGame(R4Game game, R4Game target, DropPosition position)
    {
        if (position == DropPosition.Inside || ReferenceEquals(game, target) ||
            Database is not { } database || !database.Games.Contains(game) || !database.Games.Contains(target) ||
            IsNoOpMove(database.Games, database.Games, game, target, position))
        {
            return false;
        }

        Change(null, () =>
        {
            database.Games.Remove(game);
            var index = database.Games.IndexOf(target);
            database.Games.Insert(position == DropPosition.After ? index + 1 : index, game);
        });
        return true;
    }

    public bool MoveGameBy(R4Game game, int offset)
    {
        if (Database is not { } database)
        {
            return false;
        }

        var index = database.Games.IndexOf(game) + offset;
        return offset != 0 && index >= 0 && index < database.Games.Count &&
               MoveGame(game, database.Games[index], offset < 0 ? DropPosition.Before : DropPosition.After);
    }

    /// <summary>
    /// The places the selected cheat can move to: the game's top level and every folder, except where it already is.
    /// </summary>
    public IReadOnlyList<MoveTarget> GetCheatMoveTargets()
    {
        if (SelectionKind != SelectionKind.Cheat || SelectedGame is not { } game || SelectedCheat is not { } cheat)
        {
            return [];
        }

        var currentParent = FindParentItems(game, cheat);
        var targets = new List<MoveTarget>();
        if (!ReferenceEquals(currentParent, game.Items))
        {
            targets.Add(new MoveTarget(null, TopLevelLabel));
        }

        AddFolderTargets(game.Items, prefix: string.Empty);
        return targets;

        void AddFolderTargets(List<IR4Item> items, string prefix)
        {
            foreach (var folder in items.OfType<R4Folder>())
            {
                var label = prefix + (string.IsNullOrWhiteSpace(folder.Title) ? "(untitled folder)" : folder.Title);
                if (!ReferenceEquals(folder.Items, currentParent))
                {
                    targets.Add(new MoveTarget(folder, label));
                }

                AddFolderTargets(folder.Items, label + " / ");
            }
        }
    }

    /// <summary>
    /// Moves the selected cheat to the end of a folder, or to the end of the game's top level.
    /// </summary>
    public MoveResult MoveSelectedCheat(R4Folder? destination)
    {
        if (SelectionKind != SelectionKind.Cheat || SelectedGame is not { } game || SelectedCheat is not { } cheat)
        {
            return MoveResult.NotMoved;
        }

        if (destination != null)
        {
            return MoveItem(game, cheat, destination, DropPosition.Inside);
        }

        return game.Items.LastOrDefault() is { } last
            ? MoveItem(game, cheat, last, DropPosition.After)
            : MoveResult.NotMoved;
    }

    /// <summary>
    /// True if moving <paramref name="item"/> relative to <paramref name="target"/> would leave it where it is.
    /// </summary>
    private static bool IsNoOpMove<T>(List<T> source, List<T> destination, T item, T target, DropPosition position)
    {
        if (!ReferenceEquals(source, destination))
        {
            return false;
        }

        var index = source.IndexOf(item);
        if (position == DropPosition.Inside)
        {
            return index == source.Count - 1;
        }

        // Where the item lands once it has been taken out of the list.
        var targetIndex = source.IndexOf(target);
        var newIndex = (targetIndex > index ? targetIndex - 1 : targetIndex) + (position == DropPosition.After ? 1 : 0);
        return newIndex == index;
    }

    /// <summary>
    /// The name of the list <see cref="SortSelectedContainer"/> sorts: the selected folder, the folder containing
    /// the selected cheat, or null for the game's top level.
    /// </summary>
    public string? SortContainerName =>
        SortContainer() is { Folder: { } folder } ? folder.Title : null;

    /// <summary>
    /// Sorts the selected folder's contents, or the list containing the selected cheat, or the game's top level.
    /// Folders come before cheats, then items are ordered by title.
    /// </summary>
    public void SortSelectedContainer()
    {
        if (SelectedGame is not { } game || SortContainer() is not { } container)
        {
            return;
        }

        Change(game, () => SortInPlace(container.Items, item => item is R4Folder ? 0 : 1, item => item.Title));
    }

    /// <summary>
    /// Sorts all games by title.
    /// </summary>
    public void SortGames()
    {
        if (Database is not { Games.Count: > 1 } database)
        {
            return;
        }

        Change(null, () => SortInPlace(database.Games, _ => 0, GetGameDisplayTitle));
    }

    /// <summary>
    /// Finds the list that directly contains <paramref name="item"/>, searching nested folders.
    /// </summary>
    private static List<IR4Item>? FindParentItems(R4Game game, IR4Item item)
    {
        return Search(game.Items);

        List<IR4Item>? Search(List<IR4Item> items)
        {
            if (items.Contains(item))
            {
                return items;
            }

            return items.OfType<R4Folder>().Select(folder => Search(folder.Items)).FirstOrDefault(found => found != null);
        }
    }

    private (R4Folder? Folder, List<IR4Item> Items)? SortContainer()
    {
        if (SelectedGame is not { } game)
        {
            return null;
        }

        if (SelectionKind == SelectionKind.Folder && SelectedFolder is { } folder)
        {
            return (folder, folder.Items);
        }

        if (SelectionKind == SelectionKind.Cheat && SelectedCheat is { } cheat &&
            FindParentItems(game, cheat) is { } parent && !ReferenceEquals(parent, game.Items))
        {
            return (FindFolderWithItems(game.Items, parent), parent);
        }

        return (null, game.Items);
    }

    private static R4Folder? FindFolderWithItems(List<IR4Item> items, List<IR4Item> target)
    {
        foreach (var folder in items.OfType<R4Folder>())
        {
            if (ReferenceEquals(folder.Items, target))
            {
                return folder;
            }

            if (FindFolderWithItems(folder.Items, target) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    // OrderBy is stable, so items with equal keys keep their current order.
    private static void SortInPlace<T>(List<T> items, Func<T, int> group, Func<T, string> title)
    {
        var sorted = items
            .OrderBy(group)
            .ThenBy(title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        items.Clear();
        items.AddRange(sorted);
    }
}
