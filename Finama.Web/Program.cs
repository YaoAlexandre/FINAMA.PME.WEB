using Finama.Web;
using Finama.Web.Securite;
using Finama.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Radzen;
using System.Globalization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

// ─── Culture : montants "1 250 000", dates "15/10/2026" ───────────────────────
var culture = new CultureInfo("fr-FR");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

var baseUrl = builder.Configuration["FinamaApi:BaseUrl"] ?? "https://localhost:4432/";

// ─── État applicatif ──────────────────────────────────────────────────────────
builder.Services.AddScoped<AppState>();

// ─── HttpClient unique, authentifié par AuthHeaderHandler (Bearer + X-Device-Id + refresh auto) ───
builder.Services.AddTransient<AuthHeaderHandler>();
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<AuthHeaderHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
});

// ─── Core : stockage navigateur et client API (erreurs et réseau gérés au même endroit) ───
builder.Services.AddScoped<ILocalStorage, LocalStorage>();
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<IdentifiantAppareil>();

// ─── Services API par module ──────────────────────────────────────────────────
// Authentification & tableau de bord
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITableauBordService, TableauBordService>();
// Comptabilité
builder.Services.AddScoped<IExercicesService, ExercicesService>();
builder.Services.AddScoped<IEtatsFinanciersService, EtatsFinanciersService>();
builder.Services.AddScoped<IPlanComptableService, PlanComptableService>();
builder.Services.AddScoped<IEcrituresService, EcrituresService>();
builder.Services.AddScoped<IFacturesService, FacturesService>();
builder.Services.AddScoped<ITiersService, TiersService>();
// Administration
builder.Services.AddScoped<IUtilisateursService, UtilisateursService>();
builder.Services.AddScoped<IEntrepriseService, EntrepriseService>();
// Commerce, ventes, support…
builder.Services.AddScoped<DevisApiService>();
builder.Services.AddScoped<CommerceApiService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<ModulesService>();
builder.Services.AddScoped<SupportService>();
builder.Services.AddScoped<VentesApiService>();
builder.Services.AddScoped<ITenantAdminService, TenantAdminService>();

// ─── Authentification & autorisations (mêmes noms de policies que l'API) ─────
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CustomAuthStateProvider>());
builder.Services.AjouterPermissionsFinama();
builder.Services.AddScoped<ImagesArticlesService>();


// ─── Radzen (enregistre déjà NotificationService, DialogService, TooltipService…) ───
builder.Services.AddRadzenComponents();

await builder.Build().RunAsync();
