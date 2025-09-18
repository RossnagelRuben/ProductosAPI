using BlazorApp_ProductosAPI;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<BlazorApp_ProductosAPI.Services.UbicacionService>();
builder.Services.AddScoped<BlazorApp_ProductosAPI.Services.TreeBuilderService>();
builder.Services.AddScoped<BlazorApp_ProductosAPI.Services.ProductoService>();
builder.Services.AddScoped<BlazorApp_ProductosAPI.Services.IAuthService, BlazorApp_ProductosAPI.Services.AuthService>();

await builder.Build().RunAsync();
