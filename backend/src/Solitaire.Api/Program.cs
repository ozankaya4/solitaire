var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// Skeleton only: a single liveness endpoint so the host has something to serve.
// Game endpoints will be added later and will delegate to Solitaire.Engine.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
