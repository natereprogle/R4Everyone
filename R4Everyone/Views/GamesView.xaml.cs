using Microsoft.UI.Xaml;
using R4Everyone.Services;
using R4Everyone.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace R4Everyone.Views;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class GamesView
{
    public GamesViewModel ViewModel { get; private set; }

    private readonly DialogService _dialogService;

    public GamesView()
    {
        InitializeComponent();
        _dialogService = new DialogService();
        Loaded += MainPage_Loaded;

        ViewModel = new GamesViewModel(_dialogService);
        DataContext = ViewModel;
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        _dialogService.Initialize(XamlRoot);
    }
}