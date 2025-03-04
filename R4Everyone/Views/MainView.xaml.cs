using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using R4Everyone.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using R4Everyone.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace R4Everyone.Views;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainView
{
    public MainViewModel ViewModel { get; } = new();

    public MainView()
    {
        InitializeComponent();
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is not NavigationViewItem { Tag: string pageType }) return;

        var page = Type.GetType($"R4Everyone.Views.{pageType}");

        if (page is null) return;

        MainFrame.Navigate(page);
    }
}
