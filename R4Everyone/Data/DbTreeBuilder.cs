using R4Everyone.Binary4Everyone;
using Terminal.Gui.Views;

namespace R4Everyone.Data;

public class DbTreeBuilder(bool supportsCanExpand) : ITreeBuilder<object>
{
    public bool CanExpand(object model)
    {
        return model is R4Folder or R4Game;
    }

    public IEnumerable<object> GetChildren(object model)
    {
        return model switch
        {
            R4Folder folder => folder.Items.AsEnumerable(),
            R4Game game => game.Items.AsEnumerable(),
            _ => []
        };
    }

    public bool SupportsCanExpand { get; } = supportsCanExpand;
}