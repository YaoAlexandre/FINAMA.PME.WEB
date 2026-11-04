namespace Finama.Web.Core.Securite;

/// <summary>
/// Identifiant stable de l'appareil (OTP silencieux : un appareil déjà reconnu n'a pas à ressaisir de code).
/// AuthHeaderHandler l'envoie dans l'en-tête X-Device-Id ; ce service garantit qu'il existe.
/// </summary>
public sealed class IdentifiantAppareil
{
    private readonly ILocalStorage _stockage;

    public IdentifiantAppareil(ILocalStorage stockage) => _stockage = stockage;

    /// <summary>Lit l'identifiant, en le créant à la première utilisation.</summary>
    public async Task<string> ObtenirAsync()
    {
        var id = await _stockage.LireAsync(ClesStockage.Appareil);
        if (string.IsNullOrEmpty(id))
        {
            id = Guid.NewGuid().ToString();
            await _stockage.EcrireAsync(ClesStockage.Appareil, id);
        }
        return id;
    }

    /// <summary>S'assure que l'identifiant existe avant un appel API.</summary>
    public Task GarantirAsync() => ObtenirAsync();
}
