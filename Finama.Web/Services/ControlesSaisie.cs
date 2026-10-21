using System.Text.RegularExpressions;

namespace Finama.Web.Services;

/// <summary>
/// Mêmes règles que le serveur (Finama.Core.Validation.ValidationContact), pour un message immédiat à l'écran.
/// Téléphone : norme UIT E.164 = 15 chiffres max ; saisie 17 caractères max (« + » et espaces).
/// </summary>
public static partial class ControlesSaisie
{
    public const int TelephoneSaisieMax = 17;
    public const int TelephoneChiffresMin = 8;
    public const int TelephoneChiffresMax = 15;

    [GeneratedRegex(@"^\+?[0-9 ]+$")] private static partial Regex Tel();
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$")] private static partial Regex Mail();

    /// <summary>Message d'erreur, ou null si la valeur est vide ou correcte.</summary>
    public static string? Telephone(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var t = v.Trim();
        if (t.Length > TelephoneSaisieMax) return $"{TelephoneSaisieMax} caractères maximum.";
        if (!Tel().IsMatch(t)) return "Chiffres, espaces et « + » initial uniquement.";
        var n = t.Replace(" ", "").TrimStart('+').Length;
        if (n < TelephoneChiffresMin) return $"{TelephoneChiffresMin} chiffres au minimum.";
        if (n > TelephoneChiffresMax) return $"{TelephoneChiffresMax} chiffres au maximum (norme internationale).";
        return null;
    }

    public static string? Email(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var e = v.Trim();
        return e.Length > 254 || !Mail().IsMatch(e) ? "Adresse invalide : format attendu nom@domaine.com" : null;
    }

    public static string? Nom(string? v, int min = 2, int max = 150)
    {
        if (string.IsNullOrWhiteSpace(v)) return "Obligatoire.";
        var n = v.Trim().Length;
        return n < min ? $"{min} caractères au minimum." : n > max ? $"{max} caractères au maximum." : null;
    }
}
