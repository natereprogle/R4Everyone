using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using R4Everyone;
using R4Everyone.Utils;
using R4Everyone.Utils.Bus;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Input;

ConfigurationManager.Enable(ConfigLocations.All);

using var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((_, services) =>
    {
        services.AddSingleton<IUiEventBus, UiEventBus>();
        services.AddSingleton<IR4Session, R4Session>();
        services.AddTerminalGuiViews();
    })
    .Build();

using var scope = host.Services.CreateScope();
var services = scope.ServiceProvider;

try
{
    using var app = Application.Create();
    app.Init();
    
    var r4App = services.GetRequiredService<R4EveryoneApp>();
    r4App.MouseBindings.Add(MouseFlags.RightButtonClicked, Command.Context);
    
    app.Run(r4App);
    r4App.Dispose();
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
}