using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using OhioRiverFishingPWA;
using OhioRiverFishingPWA.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register our services
builder.Services.AddScoped<RiverConditionService>();
builder.Services.AddScoped<LockScheduleService>();
builder.Services.AddScoped<FishingCalculators>();
builder.Services.AddScoped<ExternalApiProxyService>(); // Add the proxy service
builder.Services.AddMudServices();

await builder.Build().RunAsync();
