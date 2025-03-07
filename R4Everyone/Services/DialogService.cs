using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using R4Everyone.Binary4Everyone;
using R4Everyone.Views;

namespace R4Everyone.Services;

public class DialogService
{
    public XamlRoot? XamlRoot { get; private set; }

    public void Initialize(XamlRoot xamlRoot)
    {
        XamlRoot = xamlRoot;
    }

    public async Task<ContentDialogResult> ShowConfirmationAsync(string title, string content)
    {
        if (XamlRoot is null)
            throw new InvalidOperationException("DialogService must be initialized before use");

        var dialog = new ContentDialog
        {
            Title = title,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            Content = content,
            PrimaryButtonText = "Yes",
            SecondaryButtonText = "No",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        return await dialog.ShowAsync();
    }

    public async Task ShowMessageAsync(string title, string content)
    {
        if (XamlRoot is null)
            throw new InvalidOperationException("DialogService must be initialized before use");

        var dialog = new ContentDialog
        {
            Title = title,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            Content = content,
            PrimaryButtonText = "Yes",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();
    }
}