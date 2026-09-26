# Errors and failures

**Read this if:** an operation you are writing can fail, you are about to `throw`, or you are
wondering where the `try`/`catch` blocks are.

## The rule

**Exceptions are for bugs and genuinely exceptional conditions. Expected failures are return
values.**

A user asking for a record that does not exist is not exceptional — it is Tuesday. A malformed
request is not exceptional. A business rule rejecting an input is not exceptional. Those are
outcomes the endpoint's signature should name.

A null reference, a corrupt configuration, a disk that has gone away — those are exceptional, and
throwing is right, because there is nothing sensible for the caller to do.

## Why it matters more than style

Look at what an endpoint declares:

```csharp
private static Results<Ok<GreetingResponse>, ValidationProblem, ProblemHttpResult> Handle(...)
```

Every way this request can end is in that type. The compiler stops anything else escaping, and
`openapi.json` — the committed contract — lists all three. A thrown exception appears in **neither**.
It is invisible to the signature, invisible to the contract, invisible in review, and it becomes a
500 that the caller cannot distinguish from a genuine bug.

That is the argument. It is not about performance or purity; it is that a returned failure is part
of the API and a thrown one is not.

## How to return a failure

Use the typed result union. `src/Api/Features/Greetings/` is the worked example: a reserved name is
an expected rejection, so `Greeter.IsReserved` returns a `bool` and the endpoint turns it into a 409:

```csharp
if (Greeter.IsReserved(request.Name))
{
    return TypedResults.Problem(
        title: "Name is reserved.",
        detail: $"'{request.Name}' cannot be greeted.",
        statusCode: StatusCodes.Status409Conflict);
}
```

**One sharp edge.** `ProblemHttpResult` carries no compile-time status code, so nothing can infer it
and the response would be missing from `openapi.json`. Declare it on the endpoint:

```csharp
.ProducesProblem(StatusCodes.Status409Conflict);
```

Typed arms like `Ok<T>` and `ValidationProblem` are inferred and need no equivalent. Always check the
`openapi.json` diff after adding a failure path — if the status code is not there, callers do not
know it exists.

## The safety net

`src/Api/Infrastructure/ProblemDetailsExceptionHandler.cs` catches anything that escapes and turns
it into a ProblemDetails response with a `traceId`.

This is a safety net for bugs, not the error-handling strategy. If you find yourself relying on it
for an expected outcome, that outcome belongs in the return type instead.

Without it, ASP.NET Core returns a 500 with an **empty body** — no problem type, no trace id, nothing
the caller can act on or correlate with your logs. That is the out-of-the-box behaviour, and it is
why `AddProblemDetails()`, `AddExceptionHandler<T>()` and `UseExceptionHandler()` are all wired in
`Program.cs`.

The `traceId` comes from `Activity.Current`, so it is the OpenTelemetry trace id. A user reporting
"I got an error, here is the id" gives you the exact trace.

### A .NET 10 behaviour change worth knowing

`IExceptionHandler` is from .NET 8 and remains the preferred mechanism in .NET 10. What changed in
.NET 10: **when `TryHandleAsync` returns `true`, the middleware no longer emits its own logs or
metrics.** A handler that returns `true` without logging silently swallows the error, where .NET 8
and 9 would still have logged it.

The handler here logs at `Error` before returning `true`. If you write another one, log in it.

## Representing failures below the HTTP boundary

At the HTTP boundary — which is all this template currently has — `Results<T1, T2, T3>` is built in,
AOT-safe, and drives the OpenAPI contract. Nothing else is needed.

The question gets harder once you add a domain layer, because `Results<...>` is an HTTP type and
your domain should not know about HTTP. A multi-step operation that can fail partway needs its own
way to say so. These are the options, and none of them is in this template.

| Option                       | Shape                             | Cost                                                                                                               |
| ---------------------------- | --------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| `ErrorOr`                    | `ErrorOr<T>` with an error list   | Small, focused, widely used. One more dependency, and its style spreads.                                           |
| `FluentResults`              | `Result<T>` with reasons/metadata | Richer, heavier. Encourages stuffing context into results.                                                         |
| `OneOf`                      | `OneOf<T1, T2>` generic unions    | Closest to a real union type. Verbose call sites; poor exhaustiveness guarantees.                                  |
| `CSharpFunctionalExtensions` | `Result<T, E>`, `Maybe<T>`        | Opinionated in a good way if you want the whole functional style. All or nothing.                                  |
| `LanguageExt`                | Full functional prelude           | Very large. Changes how the whole codebase reads.                                                                  |
| Hand-rolled `Result<T>`      | ~40 lines you own                 | No dependency, no licence risk. You will get exhaustiveness wrong, and every team writes a slightly different one. |

## Why the template picks none of them — and why you might not either

Two reasons.

The first is the general one from [adding-a-dependency.md](adding-a-dependency.md): the style
propagates. The first slice to use a result type sets the convention for every slice after it,
because that is what people and agents copy. That makes it an unusually expensive default for a
template to impose, and there is no domain layer here for it to propagate through yet.

The second is specific and time-bound: **C# 15 adds union types, shipping with .NET 11 in November
2026.** They are the language-level version of what every library in that table approximates:

```csharp
public union GreetingOutcome(Greeted, NameReserved, NameTooLong);

var message = outcome switch
{
    Greeted g       => g.Message,
    NameReserved r  => $"'{r.Name}' is reserved.",
    NameTooLong t   => "Too long.",
};
```

The compiler enforces exhaustiveness — no discard, no `default` arm — which is exactly the guarantee
the libraries cannot give you. As of .NET 11 RC1 (September 2026) the feature is stabilised: no
`<LangVersion>preview</LangVersion>`, and C# 15 is the default for `net11.0`. The supported way to
use it is targeting .NET 11, a standard-term release, and this template deliberately targets .NET 10
LTS — so it is not something the template adopts, but it is close enough to plan around.

**The practical advice:** if you are starting a domain layer now and can wait, wait. Adopting a
result library while the language feature is at release candidate means a migration across every
slice you write in the meantime. If you cannot wait, `ErrorOr` is the smallest thing that
works and the easiest to migrate away from later.

Either way the rule above does not change. Whether a failure is a `ProblemHttpResult`, an
`ErrorOr<T>`, or a C# union, it is **returned**. The mechanism is a detail; not throwing is the
decision.

## What is enforced

- `CA1031` (do not catch general exception types) is an error. A bare `catch (Exception)` that
  swallows and continues is how "exceptional" discipline erodes in practice.
- `ProblemDetailsExceptionHandlerTests` asserts the handler responds 500, writes a non-empty body,
  and **does not leak the exception message** — messages routinely contain connection strings.
- The integration tests assert the 409 returns `application/problem+json` with a `traceId`, which
  also proves `ProblemDetails` serializes under Native AOT (it needs an entry in
  `ApiJsonSerializerContext`, and a missing one fails at runtime rather than at build).
