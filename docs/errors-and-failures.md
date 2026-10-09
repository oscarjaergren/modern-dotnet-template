# Errors and failures

**Read this if:** something you are writing can fail, you are about to `throw`, or you wonder where
the `try`/`catch` blocks are.

## The rule

**Exceptions are for bugs. Expected failures are return values.** A missing record, a malformed
request, a rejected business rule: those are outcomes, and the endpoint's signature should name them.
A null reference or a corrupt configuration is exceptional, and throwing is right.

The reason is visibility. This signature names every way the request can end, and so does the
committed `openapi.json`:

```csharp
private static Results<Ok<GreetingResponse>, ValidationProblem, ProblemHttpResult> Handle(...)
```

A thrown exception appears in neither. It becomes a 500 the caller cannot tell apart from a bug.

## Returning a failure

`Greetings` is the example. Logic below the endpoint returns an
[`ErrorOr<T>`](https://github.com/amantinband/error-or), never an HTTP type, so it stays testable
without HTTP and survives being called from more than one place:

```csharp
if (ReservedNames.Contains(name.Trim()))
{
    return Error.Conflict("Greetings.NameReserved", $"'{name}' is reserved.");
}
```

The endpoint maps it at the edge. `ToProblem()`, in `Infrastructure/ErrorProblems.cs`, is the one
place an error type becomes a status, with the description as the ProblemDetails `detail`:

```csharp
var greeting = Greeter.Greet(request.Name, request.Age);
if (greeting.IsError)
{
    return greeting.FirstError.ToProblem();
}
```

`ProblemHttpResult` has no compile-time status, so declare it on the endpoint or it is missing from
`openapi.json`: `.ProducesProblem(StatusCodes.Status409Conflict)`. `Ok<T>` and `ValidationProblem`
are inferred. Check the `openapi.json` diff after adding a failure path.

## The safety net

`ProblemDetailsExceptionHandler` turns anything that escapes into a ProblemDetails 500 with a
`traceId`, which is the OpenTelemetry trace id. Without it ASP.NET Core returns a 500 with an empty
body. It is for bugs; if you rely on it for an expected outcome, that outcome belongs in the return
type.

**.NET 10 change:** when `TryHandleAsync` returns `true`, the middleware no longer logs the
exception. A handler that doesn't log silently swallows errors. This one logs at `Error`; if you
write another, log in it too.

## Why ErrorOr

`Results<...>` is an HTTP type, so the layer below needs its own failure type, and the first slice
to pick one sets the style for every slice after it. So the template picks: `ErrorOr` is small,
AOT-clean, and its error types map one-to-one to statuses. `FluentResults` invites stuffing context
into results, `OneOf` is verbose, and `LanguageExt` or `CSharpFunctionalExtensions` change how the
whole codebase reads.

C# 15 union types will make the category redundant, with exhaustiveness the compiler enforces. They
need `net11.0`, a standard-term release; on LTS they arrive with .NET 12 in November 2027. Migrate
then: each `ErrorOr<T>` becomes a union of its outcomes.

## What is enforced

- `CA1031` (catching general exceptions) is an error.
- `ProblemDetailsExceptionHandlerTests` assert a 500 with a non-empty body that does **not** leak
  the exception message, which often contains connection strings.
- The integration tests assert the 409 is `application/problem+json` with a `traceId`, which also
  proves `ProblemDetails` serialises under Native AOT.
