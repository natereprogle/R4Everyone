using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace R4Everyone.Services;

public class DialogService
{
    private XamlRoot _xamlRoot;

    public void Initialize(XamlRoot xamlRoot)
    {
        _xamlRoot = xamlRoot;
    }

    public async Task<ContentDialogResult> ShowConfirmationAsync(string title, string content)
    {
        if (_xamlRoot is null)
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
            XamlRoot = _xamlRoot
        };

        return await dialog.ShowAsync();
    }

    public async Task ShowMessageAsync(string title, string content)
    {
        if (_xamlRoot is null)
            throw new InvalidOperationException("DialogService must be initialized before use");

        var dialog = new ContentDialog
        {
            Title = title,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            Content = content,
            PrimaryButtonText = "Yes",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _xamlRoot
        };

        await dialog.ShowAsync();
    }
}