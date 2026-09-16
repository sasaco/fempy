using FramePrintAzure;
using FrameWeb.Startup;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;

var builder = WebApplication.CreateBuilder(args);
// This launcher and the services it starts are for local development only.
builder.WebHost.UseUrls("http://127.0.0.1:7071");
builder.Services.AddControllers();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://127.0.0.1:4200", "http://localhost:4200")
        .AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddSingleton<StartupState>();
builder.Services.AddHostedService<LocalServices>();
builder.Services.AddTransient<Function1>();
builder.Services.AddTransient<Function2>();

var app = builder.Build();
app.UseCors();
app.MapGet("/", () => Results.Content(StartupPage.Html, "text/html; charset=utf-8"));
app.MapGet("/health/ready", (StartupState state) => state.Snapshot);

// Use the original HTTP handlers: local and Azure print the same request.
app.MapMethods("/api/Function1", ["GET", "POST"], async (HttpContext context, Function1 handler) =>
    await ExecuteResult(context, await handler.Run(context.Request)));
app.MapMethods("/api/Function2", ["GET", "POST"], async (HttpContext context, Function2 handler) =>
    await ExecuteResult(context, await handler.Run(context.Request)));

await app.RunAsync();

static Task ExecuteResult(HttpContext context, IActionResult result) =>
    result.ExecuteResultAsync(new ActionContext(context, new RouteData(), new ActionDescriptor()));
