using System.Net.Http.Headers;
using Blazored.LocalStorage;

namespace Finama.Web.Services;

public class JwtAuthorizationHandler : DelegatingHandler
{
    private readonly ILocalStorageService _localStorage;

    public JwtAuthorizationHandler(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Récupération du token (adaptez la clé "authToken" selon le nom utilisé dans votre app)
        var token = await _localStorage.GetItemAsync<string>("authToken");

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}