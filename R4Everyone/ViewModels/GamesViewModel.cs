using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using R4Everyone.Binary4Everyone;
using R4Everyone.Services;

namespace R4Everyone.ViewModels;

public partial class GamesViewModel : ObservableObject
{
    public ObservableCollection<R4Game> DisplayGames { get; set; } = [];
    private readonly IDialogService _dialogService;

    public R4Database R4Database = new();

    [ObservableProperty] private bool _isEditing;

    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private bool _isSaving;

    public IAsyncRelayCommand NewFileCommand { get; }
    public IAsyncRelayCommand OpenFileCommand { get; }
    public IAsyncRelayCommand SaveFileCommand { get; }

    public GamesViewModel(IDialogService dialogService)
    {
        NewFileCommand = new AsyncRelayCommand(NewFileAction);
        OpenFileCommand = new AsyncRelayCommand(OpenFileAction);
        SaveFileCommand = new AsyncRelayCommand(SaveFileAction);

        _dialogService = dialogService;
    }

    private async Task NewFileAction()
    {
        var dialogResult = ContentDialogResult.Primary;

        if (IsEditing)
        {
            dialogResult = await _dialogService.ShowConfirmationAsync("Discard changes?", "Creating a new file will result in unsaved changes being lost. Do you want to save your changes?");
        }

        if (dialogResult == ContentDialogResult.Primary && IsEditing)
        {
            await SaveFileAction();
            DisplayGames.Clear();
            IsEditing = false;
        }

        switch (dialogResult)
        {
            case ContentDialogResult.Primary:
            case ContentDialogResult.Secondary:
                IsEditing = true;

                R4Database = new R4Database();

                DisplayGames.Clear();

                OnPropertyChanged(nameof(DisplayGames));

                break;
            case ContentDialogResult.None:
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private async Task OpenFileAction()
    {
        // Implementation for creating a new file
    }

    private async Task SaveFileAction()
    {
        if (!IsEditing) throw new InvalidOperationException("Cannot save a file outside of an editing state");

        if (string.IsNullOrWhiteSpace(R4Database.R4FilePath))
        {
            var savePicker = new FileSavePicker();

            var window = App.MainWindow;

            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);

            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("R4 Database", [".dat"]);
            savePicker.SuggestedFileName = "usrcheat";

            var file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                CachedFileManager.DeferUpdates(file);
                R4Database.R4FilePath = file.Path;
                var serializer = new R4Serializer(R4Database);
                await serializer.SerializeAsync();
                var status = await CachedFileManager.CompleteUpdatesAsync(file);

                switch (status)
                {
                    case FileUpdateStatus.Complete:
                        await _dialogService.ShowMessageAsync("File saved", "The file was saved successfully");
                        break;
                    case FileUpdateStatus.CompleteAndRenamed:
                        await _dialogService.ShowMessageAsync("File saved",
                            "The file was saved successfully, but a copy was made");
                        break;
                    default:
                        await _dialogService.ShowMessageAsync("File not saved", "The file was not saved successfully");
                        break;
                }
            }
            else
            {
                await _dialogService.ShowMessageAsync("File not saved", "Save was cancelled");
            }
        }
    }
}