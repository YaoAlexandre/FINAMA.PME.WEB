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

// 1. Enregistrement des services de base
builder.Services.AddScoped<AppState>();
builder.Services.AddBlazoredLocalStorage();

// 2. Enregistrement du Handler d'authentification JWT
builder.Services.AddTransient<JwtAuthorizationHandler>();

// 3. Configuration du HttpClient global muni du Handler JWT
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<JwtAuthorizationHandler>();
    handler.InnerHandler = new HttpClientHandler();

    return new HttpClient(handler)
    {
        BaseAddress = new Uri(baseUrl)
    };
});

// 4. Enregistrement des services API (ils utiliseront automatiquement le HttpClient authentifié)
builder.Services.AddScoped<FinamaApiService>();
builder.Services.AddScoped<DevisApiService>();
builder.Services.AddScoped<ITenantAdminService, TenantAdminService>();

// 5. Authentification, Radzen et Autorisations
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<CustomAuthStateProvider>());

builder.Services.AddScoped<NotificationService>();
builder.Services.AddRadzenComponents();

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
        policy.RequireRole("AdminTenant", "Commercial", "SuperAdmin"));
});

await builder.Build().RunAsync();