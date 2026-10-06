using System.Text.Json.Serialization;
using Api;
using Api.Features.Greetings;
using Api.Features.Ping;
using Api.Infrastructure;

// The slim host omits reflection-heavy defaults that Native AOT cannot trim.
var builder = WebApplication.CreateSlimBuilder(args);

builder.AddServiceDefaults();

// Injected rather than static, so tests can use a fake clock.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    // No reflection fallback under AOT: every wire type must be in ApiJsonSerializerContext.
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiJsonSerializerContext.Default);

    // A repeated key would be last-wins, so a proxy reading the first one sees a different request.
    options.SerializerOptions.AllowDuplicateProperties = false;

    // The web default accepts "36" for an int, which types every int in openapi.json as a string too.
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});

// Must be called from the assembly that defines the endpoints, or validation silently does
// nothing. See the traps in AGENTS.md.
builder.Services.AddValidation();

// Without these, an unhandled exception is a 500 with an empty body. Every ProblemDetails carries
// a traceId linking it to its OpenTelemetry trace. See docs/errors-and-failures.md.
builder.Services.AddProblemDetails();

builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

// Also drives the build-time generation of the committed src/Api/openapi.json, the contract.
builder.Services.AddOpenApi();

var app = builder.Build();

// Must come before anything that can throw.
app.UseExceptionHandler();

// Gives a ProblemDetails body to errors returned before any endpoint code runs, such as a missing
// request body in production.
app.UseStatusCodePages();

app.MapDefaultEndpoints();

// Development only: the contract is committed, so production doesn't need to serve it.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// One line per slice, no assembly scanning: every route is visible here.
app.MapPing();
app.MapGreetings();

await app.RunAsync();
