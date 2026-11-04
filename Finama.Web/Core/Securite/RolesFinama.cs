namespace Finama.Web.Core.Securite;

/// <summary>Catalogue des profils proposés à l'administrateur de l'entreprise (valeurs = RoleUtilisateur côté API).</summary>
public static class RolesFinama
{
    public sealed record Profil(int Valeur, string Code, string Libelle, string Description, string Acces, string Icone);

    public static readonly Profil[] Attribuables =
    [
        new(0, "AdminTenant", "Administrateur (gérant)",
            "Accès total : tous les modules, les utilisateurs, les paramètres et les validations (annulation de ticket, clôture d'exercice, fin de journée).",
            "Tout", "bi-shield-lock"),
        new(1, "Comptable", "Comptable",
            "Saisit et valide les écritures, gère factures et tiers, édite balance, grand livre et états financiers. Consulte les devis.",
            "Finance & Compta", "bi-calculator"),
        new(3, "Collaborateur", "Aide-comptable",
            "Saisit des écritures en brouillon et prépare les factures. Un comptable ou le gérant valide ensuite.",
            "Finance & Compta (saisie)", "bi-pencil-square"),
        new(4, "Commercial", "Commercial / vendeur",
            "Prépare et suit les devis, gère les clients ; peut aussi tenir la caisse et le stock (profil polyvalent des petites structures).",
            "Ventes & Devis, Commerce", "bi-briefcase"),
        new(5, "Caissier", "Caissier / caissière",
            "Ouvre et clôture sa caisse, vend, encaisse, crée les clients fidélité. Ne modifie ni les articles, ni le stock, ni les prix. Une annulation de ticket demande le gérant.",
            "Caisse", "bi-cash-coin"),
        new(6, "Magasinier", "Magasinier",
            "Gère les articles, les réceptions de marchandise, les inventaires et les étiquettes. N'a pas accès à la caisse.",
            "Stock", "bi-box-seam"),
        new(2, "Lecture", "Consultation seule",
            "Consulte la comptabilité et les états, sans rien modifier (expert-comptable externe, associé, banquier).",
            "Finance & Compta (lecture)", "bi-eye"),
    ];

    public static Profil? ParCode(string? code) => Attribuables.FirstOrDefault(r => r.Code == code);

    public static string Libelle(string? code) => code == "SuperAdmin" ? "Super Admin Plateforme" : ParCode(code)?.Libelle ?? code ?? "";
}
