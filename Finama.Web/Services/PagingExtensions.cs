namespace Finama.Web;

public static class PagingExtensions
{
    /// <summary>Découpe une liste déjà chargée en mémoire (listes sans pagination côté API).</summary>
    public static IEnumerable<T> Page<T>(this IEnumerable<T> source, int page, int pageSize)
        => source.Skip((Math.Max(page, 1) - 1) * Math.Max(pageSize, 1)).Take(Math.Max(pageSize, 1));

    /// <summary>Ramène la page dans les limites valides (ex. après une suppression ou un filtre).</summary>
    public static int PageValide(int page, int totalItems, int pageSize)
    {
        var max = Math.Max(1, (int)Math.Ceiling(totalItems / (double)Math.Max(pageSize, 1)));
        return Math.Clamp(page, 1, max);
    }
}
