using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using R4Everyone.Web;
using R4Everyone.Web.Services;
using R4Everyone.Web.State;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<EditorState>();
builder.Services.AddScoped<GameTdbService>();
builder.Services.AddScoped<CoverArtService>();
builder.Services.AddScoped<ChangelogService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<FileDownloadService>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<ConfirmService>();
builder.Services.AddScoped<EditorCommands>();
builder.Services.AddScoped<KeyboardShortcutService>();

var host = builder.Build();

// Parse the GameTDB database while the loading screen is shown, so cover art lookups are instant later.
await host.Services.GetRequiredService<GameTdbService>().LoadAsync();
await host.RunAsync();
