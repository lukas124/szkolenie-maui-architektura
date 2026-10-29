using System.Net.Http.Headers;
using FieldService.Application.Session;

namespace FieldService.Infrastructure.Http;

// Żądanie do API z tokenem zalogowanego technika
internal static class AuthorizedRequest
{
    public static HttpRequestMessage Create(HttpMethod method, string path, AccessTokenStore tokens)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return request;
    }
}
