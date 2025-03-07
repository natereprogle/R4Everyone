using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using R4Everyone.Binary4Everyone;
using R4Everyone.ViewModels;

namespace R4Everyone.Views;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class GameDialogView : Page
{
    public GameDialogViewModel ViewModel { get; set; }

    public GameDialogView(R4Game game)
    {
        InitializeComponent();
        ViewModel = new GameDialogViewModel(game);
    }
}
