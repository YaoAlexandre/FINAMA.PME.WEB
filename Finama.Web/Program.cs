using Finama.Web;
using Finama.Web.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

var apiSettings = builder.Configuration.GetSection("FinamaApi");
var baseUrl = apiSettings["BaseUrl"] ?? "https://localhost:4432/";

builder.Services.AddScoped<AppState>(); // Scoped est parfait pour Blazor WASM

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(baseUrl)
});
builder.Services.AddScoped<FinamaApiService>();

await builder.Build().RunAsync();