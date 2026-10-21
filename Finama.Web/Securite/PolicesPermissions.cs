using Finama.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Finama.Web.Securite;

public sealed class PermissionRequirement(params string[] permissions) : IAuthorizationRequirement
{
    public IReadOnlyList<string> Permissions { get; } = permissions;
}

public sealed class PermissionHandler(PermissionsWeb permissions) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;
        if (context.User.IsInRole("SuperAdmin")) { context.Succeed(requirement); return; }

        var accordees = await permissions.ChargerAsync();
        if (requirement.Permissions.Any(accordees.Contains))
            context.Succeed(requirement);
    }
}

/// <summary>Mêmes noms de policies que l'API : les pages et menus existants fonctionnent sans modification.</summary>
public static class PolicesPermissions
{
    public static IServiceCollection AjouterPermissionsFinama(this IServiceCollection services)
    {
        services.AddScoped<PermissionsWeb>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();

        services.AddAuthorizationCore(o =>
        {
            void P(string nom, params string[] perms) => o.AddPolicy(nom, p => p.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(perms)));

            P("AdminTenant", "admin.parametres");
            P("Comptable", "compta.valider");
            P("Saisie", "compta.saisir");
            P("LectureSeule", "compta.consulter");
            P("Commercial", "caisse.vendre", "stock.consulter", "stock.gerer", "ventes.devis");
            P("Utilisateurs", "admin.utilisateurs");
            P("Ventes", "ventes.devis", "ventes.tousdevis");
            P("Caisse", "caisse.vendre");
            P("FinJournee", "caisse.finjournee");
            P("Stock", "stock.consulter", "stock.gerer");
            o.AddPolicy("SuperAdmin", p => p.RequireRole("SuperAdmin"));
        });

        return services;
    }
}
