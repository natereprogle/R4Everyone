using R4Everyone.Binary4Everyone;
using R4Everyone.Data;
using R4Everyone.Utils;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace R4Everyone.Views.SubViews;

public class GameDetailsSubview : View
{
    public GameDetailsSubview(R4Game game, List<LabelWithView> labels)
    {
        Height = Dim.Fill();
        Width = Dim.Fill();
        base.Visible = true;
        CanFocus = true;

        var gameTitleLabel = new Label
        {
            Text = "Title: ",
            Y = 0
        };

        var gameTitleTextField = new TextField
        {
            X = Pos.Right(gameTitleLabel),
            Y = 0,
            Width = Dim.Fill(),
            Text = game.GameTitle,
        };

        var gameIdLabel = new Label
        {
            Text = "Game ID: ",
            Y = 1
        };

        var gameIdTextField = new TextField
        {
            X = Pos.Right(gameIdLabel),
            Y = 1,
            Width = 5,
            Text = game.GameId
        };

        var gameChecksumLabel = new Label
        {
            Text = "Game Checksum: ",
            Y = 2
        };

        var gameChecksumTextField = new TextField
        {
            X = Pos.Right(gameChecksumLabel),
            Y = 2,
            Width = 9,
            Text = game.GameChecksum
        };

        var gameEnabledLabel = new Label
        {
            Text = "Enabled: ",
            Y = 3
        };

        var gameEnabledCheckbox = new CheckBox
        {
            X = Pos.Right(gameEnabledLabel),
            Y = 3,
            Value = game.GameEnabled ? CheckState.Checked : CheckState.UnChecked,
            CanFocus = false
        };

        var gameMasterCodeLabel = new Label
        {
            Text = "Master Code: ",
            Y = 4
        };

        var gameMasterCodeEditor =
            new TextView
            {
                Y = 5,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                Text = HexCheatTextCodec.FormatInitialFromFileUIntsLittleEndian(game.MasterCodes),
            };

        // Default of 20 is fine
        labels.Add(new LabelWithView("GameTitle", gameTitleLabel, gameTitleTextField));

        // IDs are always 4 characters, and checksums are always 8. Limit the field size as such
        labels.Add(new LabelWithView("GameId", gameIdLabel, gameIdTextField, 4));
        labels.Add(new LabelWithView("GameChecksum", gameChecksumLabel, gameChecksumTextField,
            8));
        labels.Add(new LabelWithView("GameEnabled", gameEnabledLabel, gameEnabledCheckbox));
        labels.Add(new LabelWithView("GameMasterCode", gameMasterCodeLabel, gameMasterCodeEditor));

        labels.ForEach(label =>
        {
            switch (label.View)
            {
                case TextField textField:
                {
                    var handling = false;
                    textField.ValueChanged += (_, args) =>
                    {
                        if (handling) return;
                        // Ensures that we can still backspace even if the title is longer than the field's max size
                        // Basically, we only check if the text length is increasing, not decreasing, in size.
                        if (args.NewValue?.Length > label.TextFieldMaxSize &&
                            !(args.NewValue?.Length < args.OldValue?.Length))
                        {
                            handling = true;
                            textField.Text = args.OldValue ?? string.Empty;
                            textField.InsertionPoint = label.TextFieldMaxSize;
                            handling = false;
                            return;
                        }

                        var val = args.NewValue ?? string.Empty;

                        switch (label.Identifier)
                        {
                            case "GameTitle":
                                game.GameTitle = val;
                                break;
                            case "GameId":
                                game.GameId = val;
                                break;
                            case "GameChecksum":
                                game.GameChecksum = val;
                                break;
                        }
                    };
                    break;
                }
                case TextView textView:
                    textView.ContentsChanged += (_, _) =>
                    {
                        try
                        {
                            game.MasterCodes = HexCheatTextCodec.ParseToFileUIntsLittleEndian(textView.Text);
                            textView.Title = string.Empty;
                        }
                        catch (FormatException e)
                        {
                            textView.Title = "Invalid Master Code";
                        }
                    };
                    break;
                case CheckBox checkbox:
                    checkbox.ValueChanged += (_, args) =>
                        game.GameEnabled = args.NewValue == CheckState.Checked;
                    break;
            }

            Add(label.Label, label.View);
        });
    }
}