namespace Finama.Web.Models;

public record PagedResult<T>(
    List<T> Items,
    int Page = 1,
    int PageSize = 15,
    int TotalItems = 0,
    int TotalPages = 0
)
{
    // Ce constructeur vide permet de faire "new PagedResult<TiersDto>()" sans erreur
    public PagedResult() : this(new List<T>(), 1, 15, 0, 0) { }
}