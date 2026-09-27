using System.Diagnostics;
using Api;
using Api.Features.Greetings;
using Api.Features.Ping;
using Api.Infrastructure;

// The slim host omits reflection-heavy defaults that Native AOT cannot trim.
var builder = WebApplication.CreateSlimBuilder(args);

builder.AddServiceDefaults();

// Injected rather than static, so tests can use a fake clock.
builder.Services.AddSingleton(TimeProvider.System);

// No reflection fallback under AOT: every wire type must be in ApiJsonSerializerContext.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiJsonSerializerContext.Default));

// Must be called from the assembly that defines the endpoints, or validation silently does
// nothing. See the traps in AGENTS.md.
builder.Services.AddValidation();

// Without these, an unhandled exception is a 500 with an empty body. traceId links the response to
// its OpenTelemetry trace. See docs/errors-and-failures.md.
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);

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
