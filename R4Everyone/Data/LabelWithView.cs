using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace R4Everyone.Data;

public record LabelWithView(string Identifier, Label Label, View View, int TextFieldMaxSize = 20);