using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using R4Everyone.Web;
using R4Everyone.Web.Services;
using R4Everyone.Web.State;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<XmlDataService>();
builder.Services.AddScoped<EditorState>();
builder.Services.AddScoped<ViewportService>();
builder.Services.AddScoped<ToastService>();


await builder.Build().RunAsync();
