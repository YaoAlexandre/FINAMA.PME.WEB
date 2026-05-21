using Blazored.LocalStorage;
using Finama.Web;
using Finama.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
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
builder.Services.AddBlazoredLocalStorage();

// Juste après ton builder

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();

builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy("Saisie", policy => policy.RequireRole("AdminTenant", "Comptable", "Collaborateur"));
    options.AddPolicy("AdminTenant", policy => policy.RequireRole("AdminTenant"));
});



await builder.Build().RunAsync();