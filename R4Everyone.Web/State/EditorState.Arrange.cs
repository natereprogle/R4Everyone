using R4Everyone.Binary4Everyone;

namespace R4Everyone.Web.State;

/// <summary>
/// A folder a cheat can be moved into, or the top level of the game when <see cref="Folder"/> is null.
/// </summary>
public sealed record MoveTarget(R4Folder? Folder, string Label);

/// <summary>
/// Reordering, moving, and sorting games, folders, and cheats.
/// </summary>
public partial class EditorState
{
    public const string TopLevelLabel = "Top level";

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
    /// True if the selected folder or cheat can move by <paramref name="offset"/> places among its siblings.
    /// </summary>
    public bool CanMoveSelected(int offset)
    {
        if (SelectedGame is not { } game || SelectedItem is not { } item ||
            FindParentItems(game, item) is not { } siblings)
        {
            return false;
        }

        var target = siblings.IndexOf(item) + offset;
        return target >= 0 && target < siblings.Count;
    }

    public void MoveSelected(int offset)
    {
        if (!CanMoveSelected(offset))
        {
            return;
        }

        var item = SelectedItem!;
        var siblings = FindParentItems(SelectedGame!, item)!;
        Change(SelectedGame, () =>
        {
            var index = siblings.IndexOf(item);
            siblings.RemoveAt(index);
            siblings.Insert(index + offset, item);
        });
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
    /// Moves the selected cheat to the end of another folder, or to the game's top level.
    /// </summary>
    /// <returns>
    /// True if the cheat was disabled because it was enabled and the destination is a One Hot folder that already
    /// has an enabled cheat.
    /// </returns>
    public bool MoveSelectedCheat(R4Folder? destination)
    {
        if (SelectionKind != SelectionKind.Cheat || SelectedGame is not { } game || SelectedCheat is not { } cheat ||
            FindParentItems(game, cheat) is not { } source)
        {
            return false;
        }

        var destinationItems = destination?.Items ?? game.Items;
        if (ReferenceEquals(source, destinationItems))
        {
            return false;
        }

        var disabled = cheat.Enabled && destination is { OneHot: true } &&
                       destination.Items.OfType<R4Cheat>().Any(other => other.Enabled);
        Change(game, () =>
        {
            source.Remove(cheat);
            if (disabled)
            {
                cheat.Enabled = false;
            }

            destinationItems.Add(cheat);
            Select(SelectionInfo.ForCheat(game, destination, cheat));
        });

        return disabled;
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
