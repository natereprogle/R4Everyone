using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using R4Everyone.Binary4Everyone;
using R4Everyone.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using R4Everyone.Views;

namespace R4Everyone.ViewModels;

public partial class GamesViewModel : ObservableObject
{
    public ObservableCollection<R4Game> DisplayGames { get; set; } = [];
    private readonly DialogService _dialogService;
    private readonly DatabaseService _databaseService;

    private bool _isEditing;
    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    private bool _isSaving;
    public bool IsSaving
    {
        get => _isSaving;
        set => SetProperty(ref _isSaving, value);
    }

    public IAsyncRelayCommand NewFileCommand { get; }
    public IAsyncRelayCommand OpenFileCommand { get; }
    public IAsyncRelayCommand SaveFileCommand { get; }

    public GamesViewModel(DialogService dialogService)
    {
        NewFileCommand = new AsyncRelayCommand(NewFileAction);
        OpenFileCommand = new AsyncRelayCommand(OpenFileAction);
        SaveFileCommand = new AsyncRelayCommand(SaveFileAction);

        _dialogService = dialogService;
        _databaseService = App.Services.GetRequiredService<DatabaseService>();
    }

    private async Task NewFileAction()
    {
        var dialogResult = ContentDialogResult.Primary;

        if (IsEditing)
        {
            dialogResult = await _dialogService.ShowConfirmationAsync("Save changes?", "Creating a new file will result in unsaved changes being lost. Do you want to save your changes?");
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

                _databaseService.R4Database = new R4Database();

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
        var dialogResult = ContentDialogResult.Primary;

        if (IsEditing)
        {
            dialogResult = await _dialogService.ShowConfirmationAsync("Save changes?", "Opening a new file will result in unsaved changes being lost. Do you want to save your changes?");
        }

        if (dialogResult == ContentDialogResult.Primary && IsEditing)
            await SaveFileAction();

        switch (dialogResult)
        {
            case ContentDialogResult.Primary:
            case ContentDialogResult.Secondary:

                var openPicker = new FileOpenPicker();

                var window = App.MainWindow;

                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

                WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);

                openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                openPicker.FileTypeFilter.Add(".dat");

                var file = await openPicker.PickSingleFileAsync();
                if (file != null)
                {
                    DisplayGames.Clear();
                    IsEditing = true;

                    _databaseService.R4Database = new R4Database(file.Path);
                    IsLoading = true;

                    try
                    {
                        await Task.Run(async () => await _databaseService.R4Database.ParseDatabaseAsync());
                        _databaseService.R4Database.Games.ForEach(DisplayGames.Add);
                    }
                    finally
                    {
                        IsLoading = false;
                    }
                }
                else
                {
                    await _dialogService.ShowMessageAsync("Operation cancelled",
                        "No file was selected");
                }

                break;
            case ContentDialogResult.None:
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private async Task SaveFileAction()
    {
        if (!IsEditing) throw new InvalidOperationException("Cannot save a file outside of an editing state");

        StorageFile? file;

        if (string.IsNullOrWhiteSpace(_databaseService.R4Database.R4FilePath))
        {
            var savePicker = new FileSavePicker();

            var window = App.MainWindow;

            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);

            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("R4 Database", [".dat"]);
            savePicker.SuggestedFileName = "usrcheat";

            file = await savePicker.PickSaveFileAsync();
            if (file is null)
            {
                await _dialogService.ShowMessageAsync("File not saved", "Save was cancelled");
                return;
            }
        }
        else
        {
            file = await StorageFile.GetFileFromPathAsync(_databaseService.R4Database.R4FilePath);
        }

        CachedFileManager.DeferUpdates(file);
        _databaseService.R4Database.R4FilePath = file.Path;
        var serializer = new R4Serializer(_databaseService.R4Database);
        await serializer.SerializeAsync();
        var status = await CachedFileManager.CompleteUpdatesAsync(file);

        // Why is this even necessary, since this is literally what default is for?
        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
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

    public async Task OnGameClick(object sender, ItemClickEventArgs e)
    {
        if (sender is not null)
        {
            if (e.ClickedItem is not R4Game game)
                return;

            if (_dialogService.XamlRoot is null)
                throw new InvalidOperationException("DialogService must be initialized before use");

            var appWindow = App.MainWindow ?? throw new ApplicationException("Can't open a dialog if the main window is null");

            var dialog = new ContentDialog
            {
                Title = $"{game.GameTitle}",
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                XamlRoot = _dialogService.XamlRoot,
                Width = App.MainWindow.Bounds.Width,
                Height = App.MainWindow.Bounds.Height,
                DataContext = game,
                PrimaryButtonText = "Done"
            };

            dialog.Content = new GameDialogView(game);

            await dialog.ShowAsync();
        }
        else
        {
            throw new ArgumentNullException(nameof(sender));
        }
    }
}