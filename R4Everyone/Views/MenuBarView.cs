using System.Text;
using R4Everyone.Utils;
using R4Everyone.Utils.Bus;
using Terminal.Gui.Input;
using Terminal.Gui.Views;
using OpenMode = Terminal.Gui.Views.OpenMode;

namespace R4Everyone.Views;

public class MenuBarView : MenuBar
{
    private readonly IUiEventBus _eventBus;

    public MenuBarView(IR4Session session, IUiEventBus eventBus)
    {
        _eventBus = eventBus;

        Menus =
        [
            new MenuBarItem("File", [
                new MenuItem("New", "Create a new R4 file", () => { }, Key.N.WithCtrl),
                new MenuItem("Open", "Open an existing R4 file", async void () =>
                {
                    try
                    {
                        await CreateOpenDialog(session);
                    }
                    catch (Exception)
                    {
                        MessageBox.ErrorQuery(App!, "Error While Opening",
                            "An unexpected error occurred when opening the file",
                            "Close");
                    }
                }, Key.O.WithCtrl),
                new Line(),
                new MenuItem("Save", "Save R4 file", () => { }, Key.S.WithCtrl),
                new MenuItem("Save As...", "Save R4 file as", () => { }, Key.S.WithCtrl.WithShift),
                new Line(),
                new MenuItem("Close", "Close R4 file", async void () =>
                {
                    try
                    {
                        await CloseDatabase(session);
                    }
                    catch (Exception)
                    {
                        MessageBox.ErrorQuery(App!, "Error While Closing",
                            "An unexpected error occurred when closing the file",
                            "Close");
                    }
                }, Key.W.WithCtrl),
                new MenuItem("Exit", "Exit R4Everyone", () => App!.RequestStop(), Key.Q.WithCtrl),
            ]),
            new MenuBarItem("Help", new MenuItem[]
            {
                new("About", "About R4Everyone", () =>
                {
                    var sb = new StringBuilder();
                    sb.AppendLine(@"  ___ _ _  ___                               ");
                    sb.AppendLine(@" | _ \ | || __|_ _____ _ _ _  _ ___ _ _  ___ ");
                    sb.AppendLine(@" |   /_  _| _|\ V / -_) '_| || / _ \ ' \/ -_)");
                    sb.AppendLine(@" |_|_\ |_||___|\_/\___|_|  \_, \___/_||_\___|");
                    sb.AppendLine(@"                           |__/              ");
                    MessageBox.Query(App!, 45, 5, "About", sb.ToString(), "Ok");
                }),
                new("Website", "R4Everyone Website",
                    () => BrowserHelper.OpenUrl("https://github.com/natereprogle/R4Everyone")),
                new("Report a Bug", "Opens the R4Everyone bug tracker",
                    () => BrowserHelper.OpenUrl("https://github.com/natereprogle/R4Everyone/issues")),
            })
        ];

        foreach (var subView in SubViews)
            if (subView is MenuBarItem menuBarItem)
                menuBarItem.Activating += HandleMenuBarItemActivating;

        eventBus.Subscribe<OpenFile>(async void (_) =>
        {
            try
            {
                await CreateOpenDialog(session);
            }
            catch (Exception)
            {
                MessageBox.ErrorQuery(App!, "Error", "An unexpected error occurred when opening the file", "Close");
            }
        });
    }

    #region Menu Workaround

    /// <summary>
    /// Workaround for Terminal.Gui v2 bug #4473 - MenuBar mouse clicks don't propagate correctly.
    /// When a MenuBarItem is activated (clicked), we manually invoke Command.Accept on the MenuBar
    /// to trigger the proper menu opening behavior.
    /// </summary>
    private void HandleMenuBarItemActivating(object? sender, CommandEventArgs args)
    {
        if (sender is not MenuBarItem menuBarItem || args.Context?.Binding is not MouseBinding) return;

        // Create a context with the MenuBarItem as the source
        var ctx = new CommandContext
        {
            Command = Command.Accept,
            Source = menuBarItem,
            Binding = args.Context.Binding
        };

        // Invoke Accept on the MenuBar, which will trigger OnAccepting and open the menu
        InvokeCommand(Command.Accept, ctx);

        // Mark as handled to prevent further processing
        args.Handled = true;
    }

    #endregion

    private async Task CreateOpenDialog(IR4Session session)
    {
        if (session.IsDirty)
        {
            await CloseDatabase(session);
        }

        using FileDialog fd = new()
        {
            OpenMode = OpenMode.File,
            MustExist = true,
        };

        fd.AllowedTypes.Add(new AllowedType("R4 File", ".dat"));
        var result = App!.Run(fd) as int?;
        var path = fd.Path;
        if (result is null or 1)
        {
            MessageBox.Query(App, "Cancelled", string.Empty);
        }

        await session.OpenAsync(path);
        _eventBus.Publish(new FileOpened(path, session.Current));
    }

    private async Task CloseDatabase(IR4Session session)
    {
        if (!session.IsDirty)
        {
            await session.CloseAsync();
        }
        else
        {
            var confirmation = MessageBox.Query(App!, "Unsaved Changes",
                "You have unsaved changes. Are you sure you want to open a new file? Any unsaved changes will be lost.",
                buttons:
                [
                    "Cancel",
                    "Yes"
                ]);
            if (confirmation == 1)
            {
                await session.CloseAsync();
            }
        }

        _eventBus.Publish(new FileClosed());
    }
}