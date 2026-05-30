using Blazored.LocalStorage;
using Finama.Web;
using Finama.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Radzen;

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
builder.Services.AddScoped<DevisApiService>();
builder.Services.AddBlazoredLocalStorage();

// Juste après ton builder

builder.Services.AddCascadingAuthenticationState();
//builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddRadzenComponents();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<CustomAuthStateProvider>());

builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy("AdminTenant", policy =>
        policy.RequireRole("AdminTenant", "SuperAdmin"));

    options.AddPolicy("Saisie", policy =>
        policy.RequireRole("AdminTenant", "Comptable", "Collaborateur", "SuperAdmin"));

    options.AddPolicy("Comptable", policy =>
        policy.RequireRole("AdminTenant", "Comptable", "SuperAdmin"));

    options.AddPolicy("LectureSeule", policy =>
        policy.RequireRole("AdminTenant", "Comptable", "Collaborateur", "Lecture", "Commercial", "SuperAdmin"));

    options.AddPolicy("Commercial", policy =>
        policy.RequireRole("AdminTenant", "Commercial", "SuperAdmin")); // ? nouveau
});



await builder.Build().RunAsync();