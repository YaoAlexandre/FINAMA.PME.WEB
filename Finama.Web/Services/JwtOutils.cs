using System.Text.Json;

namespace Finama.Web.Services;

/// <summary>Lecture du payload JWT côté client (affichage / expiration uniquement — aucune confiance de sécurité).</summary>
public static class JwtOutils
{
    public static Dictionary<string, JsonElement> LirePayload(string jwt)
    {
        var parties = jwt.Split('.');
        if (parties.Length < 2) return new();

        var base64 = parties[1].Replace('-', '+').Replace('_', '/');
        base64 = (base64.Length % 4) switch { 2 => base64 + "==", 3 => base64 + "=", _ => base64 };

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Convert.FromBase64String(base64)) ?? new();
        }
        catch
        {
            return new();
        }
    }

    /// <summary>Vrai si le jeton est expiré (ou expire dans moins de <paramref name="margeSecondes"/>).</summary>
    public static bool EstExpire(string jwt, int margeSecondes = 30)
    {
        var payload = LirePayload(jwt);
        if (!payload.TryGetValue("exp", out var exp) || !exp.TryGetInt64(out var secondes)) return true;
        return DateTimeOffset.FromUnixTimeSeconds(secondes) <= DateTimeOffset.UtcNow.AddSeconds(margeSecondes);
    }
}
