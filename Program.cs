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

// Servicios para Imagen.razor
builder.Services.AddScoped<BlazorApp_ProductosAPI.Services.ILocalStorageService, BlazorApp_ProductosAPI.Services.LocalStorageService>();
builder.Services.AddScoped<BlazorApp_ProductosAPI.Services.IImageFileService, BlazorApp_ProductosAPI.Services.ImageFileService>();
builder.Services.AddScoped<BlazorApp_ProductosAPI.Services.IOcrService, BlazorApp_ProductosAPI.Services.OcrService>();
builder.Services.AddScoped<BlazorApp_ProductosAPI.Services.IGeminiService, BlazorApp_ProductosAPI.Services.GeminiService>();
builder.Services.AddScoped<BlazorApp_ProductosAPI.Services.IColectorService, BlazorApp_ProductosAPI.Services.ColectorService>();
builder.Services.AddScoped<BlazorApp_ProductosAPI.Services.IJsInteropService, BlazorApp_ProductosAPI.Services.JsInteropService>();

// Asignar imágenes (SOLID: interfaces y implementaciones)
builder.Services.AddScoped<BlazorApp_ProductosAPI.Services.AsignarImagenes.IProductoQueryService, BlazorApp_ProductosAPI.Services.AsignarImagenes.ProductoQueryService>();
builder.Services.AddScoped<BlazorApp_ProductosAPI.Services.AsignarImagenes.IProductImageService, BlazorApp_ProductosAPI.Services.AsignarImagenes.ProductImageService>();

await builder.Build().RunAsync();
