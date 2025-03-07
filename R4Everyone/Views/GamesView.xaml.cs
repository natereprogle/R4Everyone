using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using R4Everyone.Services;
using R4Everyone.ViewModels;
using System.Linq;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace R4Everyone.Views;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class GamesView : Page
{
    public GamesViewModel ViewModel { get; private set; }

    private readonly DialogService _dialogService;

    public GamesView()
    {
        InitializeComponent();

        _dialogService = App.Services.GetRequiredService<DialogService>();
        Loaded += MainPage_Loaded;

        ViewModel = new GamesViewModel(_dialogService);
        DataContext = ViewModel;

    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        _dialogService.Initialize(XamlRoot);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (!ViewModel.DisplayGames.Any())
        {
            ViewModel.DisplayGames.Clear();
            var games = App.Services.GetRequiredService<DatabaseService>().R4Database.Games;
            if (games.Count == 0)
                return;

            ViewModel.IsLoading = true;

            foreach (var game in App.Services.GetRequiredService<DatabaseService>().R4Database.Games)
            {
                ViewModel.DisplayGames.Add(game);
            }

            ViewModel.IsEditing = true;
            ViewModel.IsLoading = false;
        }
    }
}