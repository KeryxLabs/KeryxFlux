namespace KeryxFlux.Domain.Models.Http
{
    public readonly record struct HttpGetRequest(Uri BaseAddress, string AuthHeader, string Path, int Timeout)
    {
        public static HttpGetRequest FromServerRequest(HttpServerRequest request, string path) =>
            new(request.RequestUri, request.AuthorizationHeader, path, request.Timeout);


    }

}
