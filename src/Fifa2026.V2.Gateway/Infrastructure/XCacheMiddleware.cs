namespace Fifa2026.V2.Gateway.Infrastructure;

/// <summary>
/// AC-6 — Garante o header default <c>X-Cache: MISS</c> nas respostas que NÃO
/// vieram do Output Cache. Usa <c>Response.OnStarting</c> (executado antes do
/// flush, quando os headers ainda são graváveis), e só seta MISS se o
/// <see cref="XCacheOutputCachePolicy"/> ainda não marcou HIT — evitando o erro
/// de escrever em headers já commitados pelo YARP no caminho de MISS.
/// </summary>
public sealed class XCacheMiddleware
{
    private readonly RequestDelegate _next;

    public XCacheMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var httpContext = (HttpContext)state;
            var headers = httpContext.Response.Headers;
            if (!headers.ContainsKey(XCacheOutputCachePolicy.CacheHitHeader))
            {
                headers[XCacheOutputCachePolicy.CacheHitHeader] = "MISS";
            }
            return Task.CompletedTask;
        }, context);

        return _next(context);
    }
}
