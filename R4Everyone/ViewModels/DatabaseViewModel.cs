using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using R4Everyone.Services;

namespace R4Everyone.ViewModels;

public partial class DatabaseViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;
    public string DatabaseTitle
    {
        get
        {
            return _databaseService.R4Database.Title;
        }
        set
        {
            _databaseService.R4Database.Title = value;
        }
    }

    public bool IsEnabled
    {
        get
        {
            return _databaseService.R4Database.Enabled;
        }
        set
        {
            _databaseService.R4Database.Enabled = value;
        }
    }


    public string GamesCountMessage => $"Total games: {_databaseService.R4Database.Games.Count}";

    public DatabaseViewModel()
    {
        _databaseService = App.Services.GetRequiredService<DatabaseService>();
    }
}
