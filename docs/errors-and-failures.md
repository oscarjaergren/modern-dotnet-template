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

`Greetings` is the example. A reserved name is expected, so `Greeter.IsReserved` returns a `bool`
and the endpoint returns a 409:

```csharp
if (Greeter.IsReserved(request.Name))
{
    return TypedResults.Problem(
        title: "Name is reserved.",
        detail: $"'{request.Name}' cannot be greeted.",
        statusCode: StatusCodes.Status409Conflict);
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

## Below the HTTP boundary

`Results<...>` is an HTTP type, so a domain layer needs its own way to report failure. None of these
is in the template:

| Option                       | Cost                                                         |
| ---------------------------- | ------------------------------------------------------------ |
| `ErrorOr`                    | Small and focused. One dependency, and its style spreads.    |
| `FluentResults`              | Richer and heavier; invites stuffing context into results.   |
| `OneOf`                      | Nearest to a real union; verbose, weak exhaustiveness.       |
| `CSharpFunctionalExtensions` | Good if you want the whole functional style. All or nothing. |
| `LanguageExt`                | Very large; changes how the whole codebase reads.            |
| Hand-rolled `Result<T>`      | No dependency. You will get exhaustiveness wrong.            |

The template picks none, for two reasons. The first slice to use one sets the style for every slice
after it. And C# 15 union types, in .NET 11 in November 2026, make the category largely redundant,
with exhaustiveness the compiler enforces:

```csharp
public union GreetingOutcome(Greeted, NameReserved, NameTooLong);

var message = outcome switch
{
    Greeted g      => g.Message,
    NameReserved r => $"'{r.Name}' is reserved.",
    NameTooLong t  => "Too long.",
};
```

They were stabilised in .NET 11 RC1 (September 2026), but they need `net11.0`, a standard-term
release, and this template targets .NET 10 LTS. If you can wait for them, wait; adopting a library
now means a migration later. If you can't, `ErrorOr` is the smallest and easiest to leave.

## What is enforced

- `CA1031` (catching general exceptions) is an error.
- `ProblemDetailsExceptionHandlerTests` assert a 500 with a non-empty body that does **not** leak
  the exception message, which often contains connection strings.
- The integration tests assert the 409 is `application/problem+json` with a `traceId`, which also
  proves `ProblemDetails` serialises under Native AOT.
