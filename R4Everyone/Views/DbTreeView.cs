using R4Everyone.Binary4Everyone;
using R4Everyone.Data;
using R4Everyone.Utils;
using R4Everyone.Utils.Bus;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace R4Everyone.Views;

public class DbTreeView : TreeView<object>
{
    public DbTreeView(IUiEventBus eventBus)
    {
        BorderStyle = LineStyle.Rounded;
        Width = Dim.Fill();
        Height = Dim.Fill();
        TreeBuilder = new DbTreeBuilder(true);
        AspectGetter = o =>
        {
            return o switch
            {
                R4Game game => game.GameTitle,
                R4Folder folder => folder.Title,
                R4Cheat cheat => cheat.Title,
                _ => o.ToString()
            };
        };

        var label = new Label
        {
            Title = "Welcome to R4Everyone!",
            X = Pos.Center()
        };

        var button = new Button
        {
            Title = "Click Me to Open an R4 Database",
            X = Pos.Center(),
            Y = Pos.Center()
        };

        button.Accepting += (_, e) =>
        {
            eventBus.Publish(new OpenFile());
            e.Handled = true;
        };

        Add(label, button);

        SelectionChanged += (_, args) => eventBus.Publish(new SelectionChange(args.NewValue));

        eventBus.Subscribe<FileOpened>(args =>
        {
            Width = Dim.Percent(50);
            RemoveAll();
            AddObjects(args.Database?.Games);
            SetNeedsDraw();
        });

        eventBus.Subscribe<FileClosed>(_ =>
        {
            Width = Dim.Fill();
            RemoveAll();
            ClearObjects();
            Add(label, button);
            SetNeedsDraw();
        });

        SetFocus();
    }
}