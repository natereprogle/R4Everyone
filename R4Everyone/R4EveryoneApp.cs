using R4Everyone.Views;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace R4Everyone;

// R4EveryoneApp *is* instantiated via `var r4App = services.GetRequiredService<R4EveryoneApp>();` in Program.cs
// ReSharper disable once ClassNeverInstantiated.Global
public class R4EveryoneApp : Runnable
{
    public R4EveryoneApp(MainWindowView mainWindowView, MenuBarView menuBarView)
    {
        mainWindowView.Y = Pos.Y(menuBarView) + 1;
        Add(mainWindowView, menuBarView);
    }
}