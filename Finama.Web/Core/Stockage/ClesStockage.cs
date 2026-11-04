namespace Finama.Web.Core.Stockage;

/// <summary>Toutes les clés du localStorage utilisées par l'application, en un seul endroit.</summary>
public static class ClesStockage
{
    // ─── Session ───
    public const string Jeton = "finama_token";
    public const string Refresh = "finama_refresh";
    public const string Appareil = "finama_device_id";
    public const string Utilisateur = "finama_user";
    public const string Entreprise = "finama_entreprise";
    public const string Devise = "finama_devise";
    public const string Logo = "finama_logo";

    public const string Permissions = "finama_permissions";

    // ─── Commerce ───
    public const string ImagesArticles = "finama_images_articles";
    public const string MagasinId = "finama_magasin_id";
    public const string MagasinNom = "finama_magasin_nom";
    public const string CaisseImages = "finama_caisse_images";
    public const string ImpressionAuto = "finama_pos_impression_auto";

    // ─── Navigation / interface ───
    public const string DernierePage = "finama_derniere_page";
    public const string PwaMasque = "finama_pwa_masque";

    /// <summary>Dernière page visitée dans un module (reprise là où l'utilisateur s'était arrêté).</summary>
    public static string PageModule(string module) => $"finama_page_{module}";

    /// <summary>Données de session à effacer à la déconnexion.</summary>
    public static readonly string[] Session = { Jeton, Refresh, Utilisateur, Entreprise, Devise };
}
