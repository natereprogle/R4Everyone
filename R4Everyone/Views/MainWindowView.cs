using System.Text;
using R4Everyone.Binary4Everyone;
using R4Everyone.Data;
using R4Everyone.Utils.Bus;
using R4Everyone.Views.SubViews;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace R4Everyone.Views;

public class MainWindowView : Window
{
    public MainWindowView(DbTreeView treeView, IUiEventBus eventBus)
    {
        BorderStyle = LineStyle.Rounded;
        Title = "R4Everyone";
        Add(treeView);

        var controlsFrame = new DbControlsFrame
        {
            Title = "Controls",
            X = Pos.Right(treeView),
            Y = Pos.AnchorEnd(),
            Height = 7,
            Width = Dim.Fill()
        };
        Add(controlsFrame);

        var detailsFrame = new DetailsFrame
        {
            X = Pos.Right(treeView),
            Y = Pos.Top(treeView),
            Width = Dim.Fill(),
            Height = Dim.Fill(7)
        };
        Add(detailsFrame);

        eventBus.Subscribe<FileOpened>(_ =>
        {
            controlsFrame.Visible = true;
            detailsFrame.Visible = true;

            controlsFrame.AddGameButton.Enabled = true;
        });
        
        eventBus.Subscribe<SelectionChange>(change =>
        {
            detailsFrame.R4Object = change.selection;

            controlsFrame.AddFolderButton.Enabled = change.selection is R4Game;
            controlsFrame.AddCheatButton.Enabled = change.selection is not R4Cheat;
            controlsFrame.RemoveGameButton.Enabled = change.selection is R4Game;
            controlsFrame.RemoveFolderButton.Enabled = change.selection is R4Folder;
            controlsFrame.RemoveCheatButton.Enabled = change.selection is R4Cheat;
        });
        
        eventBus.Subscribe<FileClosed>(_ =>
        {
            controlsFrame.Visible = false;

            detailsFrame.RemoveAll();
            detailsFrame.Visible = false;
            detailsFrame.R4Object = null;
        });
    }

    private class DetailsFrame : FrameView
    {
        private readonly List<LabelWithView> _labels = [];

        public DetailsFrame()
        {
            Title = "Details";
            base.Visible = false;
            CanFocus = true;
        }

        public object? R4Object
        {
            get;
            set
            {
                field = value;
                StringBuilder? sb = null;
                switch (field)
                {
                    case R4Game game:
                        Title = "Game Details";
                        RemoveAll();
                        _labels.Clear();
                        Add(new GameDetailsSubview(game, _labels));
                        break;
                    case R4Folder folder:
                        RemoveAll();
                        _labels.Clear();
                        Title = "Folder Details";
                        sb = new StringBuilder();
                        sb.AppendLine($"Description: {folder.Description}");
                        sb.AppendLine($"Item Count: {folder.Items.Count}");
                        break;
                    case R4Cheat cheat:
                        RemoveAll();
                        _labels.Clear();
                        Title = "Cheat Details";
                        sb = new StringBuilder();
                        sb.AppendLine($"Description: {cheat.Description}");
                        sb.AppendLine($"Enabled: {cheat.Enabled}");
                        sb.AppendLine("Code:");
                        var chunkStr = new StringBuilder();

                        for (var i = 0; i < cheat.Code.Count; i += 2)
                        {
                            chunkStr.Append(string.Join(" ",
                                cheat.Code[i].Select(b => b.ToString("X2"))));

                            if (i + 1 < cheat.Code.Count)
                            {
                                chunkStr.Append("    ");
                                chunkStr.Append(string.Join(" ",
                                    cheat.Code[i + 1].Select(b => b.ToString("X2"))));
                            }

                            chunkStr.AppendLine();
                        }

                        sb.AppendLine(chunkStr.ToString().TrimEnd());
                        break;
                    default:
                        Title = $"Unknown Object";
                        break;
                }

                Text = sb?.ToString() ?? string.Empty;
            }
        }
    }

    private class DbControlsFrame : FrameView
    {
        public readonly Button AddGameButton;
        public readonly Button AddCheatButton;
        public readonly Button AddFolderButton;
        public readonly Button RemoveGameButton;
        public readonly Button RemoveCheatButton;
        public readonly Button RemoveFolderButton;

        public DbControlsFrame()
        {
            Title = "Controls";
            base.Visible = false;
            CanFocus = false;

            AddGameButton = new Button
            {
                Title = "Add Game",
                Y = Pos.Center(),
                Enabled = false,
            };

            AddCheatButton = new Button
            {
                Title = "Add Cheat",
                Y = Pos.Center(),
                X = Pos.Right(AddGameButton),
                Enabled = false,
            };

            AddFolderButton = new Button
            {
                Title = "Add Folder",
                Y = Pos.Center(),
                X = Pos.Right(AddCheatButton),
                Enabled = false,
            };

            RemoveGameButton = new Button
            {
                Title = "Remove Game",
                Y = Pos.Bottom(AddGameButton),
                Enabled = false,
            };

            RemoveCheatButton = new Button
            {
                Title = "Remove Cheat",
                Y = Pos.Bottom(AddCheatButton),
                X = Pos.Right(RemoveGameButton),
                Enabled = false,
            };

            RemoveFolderButton = new Button
            {
                Title = "Remove Folder",
                Y = Pos.Bottom(AddFolderButton),
                X = Pos.Right(RemoveCheatButton),
                Enabled = false,
            };

            Add(AddGameButton, AddCheatButton, AddFolderButton, RemoveGameButton, RemoveCheatButton,
                RemoveFolderButton);
        }
    }
}