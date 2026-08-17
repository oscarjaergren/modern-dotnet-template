using System.Diagnostics;
using Api;
using Api.Features.Greetings;
using Api.Features.Ping;
using Api.Infrastructure;

// CreateSlimBuilder (not CreateBuilder) is the Native-AOT-friendly host: it omits the
// reflection-heavy defaults that would otherwise pull in trim-unsafe code paths.
var builder = WebApplication.CreateSlimBuilder(args);

builder.AddServiceDefaults();

// Registered rather than used statically so tests can substitute a fake clock.
builder.Services.AddSingleton(TimeProvider.System);

// Source-generated JSON. Under Native AOT there is no reflection-based fallback, so every
// type that crosses the wire must be declared in ApiJsonSerializerContext.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiJsonSerializerContext.Default));

// Built-in minimal API validation (.NET 10). Source-generated, so it is AOT-safe and
// FluentValidation stays out of the template.
// IMPORTANT: this must be called from the same assembly that defines the endpoints,
// otherwise validation silently does nothing. AGENTS.md documents this and the other
// validation trap (request types must be public).
builder.Services.AddValidation();

// Exceptions are the safety net, not the mechanism — see docs/errors-and-failures.md.
// Without these two lines an unhandled exception returns a 500 with an *empty body*: no problem
// type, no trace id, nothing the caller can act on. The traceId extension ties a failed request
// to its OpenTelemetry trace, which is the whole point of having ServiceDefaults wired up.
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);

builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

// Two separate things share this one registration, and it is worth knowing which is which:
//   1. Build-time generation — Microsoft.Extensions.ApiDescription.Server invokes the document
//      service during build and writes src/Api/openapi.json, which is committed and drift-gated
//      in CI. This needs AddOpenApi() but never touches the HTTP pipeline.
//   2. The runtime /openapi/v1.json endpoint — MapOpenApi() below, useful for pointing tooling
//      at a running service.
// The committed document is the contract; the endpoint is a convenience.
builder.Services.AddOpenApi();

var app = builder.Build();

// Must come before anything that can throw.
app.UseExceptionHandler();

app.MapDefaultEndpoints();

// Development only. The document is already committed at src/Api/openapi.json, so serving it
// from production buys nothing and publishes your full API surface to anyone who asks.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// One line per slice. No assembly scanning: an agent (or a reader) can see every route
// the app serves from this file alone, and go straight to the folder that owns it.
app.MapPing();
app.MapGreetings();

await app.RunAsync();

/// <summary>Exposed so the integration tests can reference the entry point assembly.</summary>
public partial class Program;
