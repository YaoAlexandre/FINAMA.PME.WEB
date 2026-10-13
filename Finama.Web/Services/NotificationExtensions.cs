using Radzen;

namespace Finama.Web;

/// <summary>
/// Notifications uniformes pour toute l'application.
/// Règles : succès court (4 s), erreur plus longue (8 s) avec la marche à suivre,
/// jamais de message technique brut, un seul vocabulaire.
/// Usage : @inject NotificationService Notif  puis  Notif.Succes("Écriture validée.");
/// </summary>
public static class NotificationExtensions
{
    public static void Succes(this NotificationService n, string message, string titre = "Enregistré")
        => Afficher(n, NotificationSeverity.Success, titre, message, 4000);

    public static void Erreur(this NotificationService n, string message, string titre = "Action impossible")
        => Afficher(n, NotificationSeverity.Error, titre, message, 8000);

    public static void Avertir(this NotificationService n, string message, string titre = "À vérifier")
        => Afficher(n, NotificationSeverity.Warning, titre, message, 6000);

    public static void Information(this NotificationService n, string message, string titre = "Information")
        => Afficher(n, NotificationSeverity.Info, titre, message, 4000);

    /// <summary>Erreur réseau ou serveur : message clair, sans détail technique.</summary>
    public static void ErreurServeur(this NotificationService n, string titre = "Serveur injoignable")
        => Erreur(n, "Impossible de joindre le serveur. Vérifiez votre connexion puis réessayez.", titre);

    private static void Afficher(NotificationService n, NotificationSeverity severite, string titre, string message, double dureeMs)
        => n.Notify(new NotificationMessage { Severity = severite, Summary = titre, Detail = message, Duration = dureeMs });
}
