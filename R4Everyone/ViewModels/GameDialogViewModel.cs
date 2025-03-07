using CommunityToolkit.Mvvm.ComponentModel;
using R4Everyone.Binary4Everyone;

namespace R4Everyone.ViewModels;

public partial class GameDialogViewModel(R4Game game) : ObservableObject
{
    public R4Game Game = game;
}
