using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;
using R4Everyone.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace R4Everyone.Views;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainView : Page
{
    public MainViewModel ViewModel { get; } = new();

    private static readonly Dictionary<string, Type> _viewMap = new()
    {
        ["GamesView"] = typeof(GamesView),
        ["DatabaseView"] = typeof(DatabaseView),
        //["FindGamesView"] = typeof(SettingsView)
    };

    public MainView()
    {
        InitializeComponent();
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is not NavigationViewItem { Tag: string pageTag }) return;

        if (_viewMap.TryGetValue(pageTag, out Type? pageType)) {
            MainFrame.Navigate(pageType);
        }
    }
}
