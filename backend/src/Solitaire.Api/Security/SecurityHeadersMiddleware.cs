namespace Solitaire.Api.Security;

/// <summary>
/// Adds strict security response headers to every response. The server has two
/// personalities: <c>/api/*</c> (and <c>/health</c>) return only JSON, so they get
/// a deny-everything CSP; every other path serves the bundled SPA and gets a
/// policy that allows exactly what the app uses — same-origin scripts, styles,
/// fonts, images, fetches and the service worker, nothing external.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    // No HTML/scripts on API routes; forbid everything by default.
    private const string ApiCsp =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    // The SPA is fully self-contained (self-hosted fonts, bundled engine).
    // 'unsafe-inline' script: the anti-FOUC theme snippet inlined in index.html.
    // 'unsafe-inline' style: motion (framer-motion) animates via style attributes.
    private const string SpaCsp =
        "default-src 'self'; script-src 'self' 'unsafe-inline'; "
        + "style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; "
        + "connect-src 'self'; manifest-src 'self'; worker-src 'self'; object-src 'none'; "
        + "frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        bool isApi =
            context.Request.Path.StartsWithSegments("/api")
            || context.Request.Path.StartsWithSegments("/health");
        headers["Content-Security-Policy"] = isApi ? ApiCsp : SpaCsp;

        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), microphone=(), payment=()";
        // Do not leak the server banner.
        headers.Remove("Server");

        return next(context);
    }
}

public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityHeadersMiddleware>();
}
