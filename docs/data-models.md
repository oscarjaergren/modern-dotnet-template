# Data models

**Read this if:** you are defining a type that mostly holds data — a request, a response, a value
object, an identifier, a configuration shape.

## The rule

**If a type is data, it is a `record`.** Not a class with properties, not a POCO with a constructor
you wrote by hand.

```csharp
public sealed record GreetingResponse(string Message, DateTimeOffset GreetedAt);
```

That one line gives you value equality, a sensible `ToString()`, deconstruction, `with` expressions,
and immutability. The hand-written class equivalent is forty lines that someone eventually gets
wrong — usually `Equals` and `GetHashCode`, and usually silently.

Value equality is not a nicety here. It is what makes `Assert.Equal(expected, actual)` compare
*contents* rather than references, which is the difference between a readable test and one that
constructs the expected object and then compares six fields by hand.

## Reference or struct

Default to `record` (a class). Reach for `readonly record struct` when **all** of these hold:

- The type is small — the usual guidance is 16 bytes or less.
- It is genuinely a value: an identifier, a quantity, a coordinate, a unit.
- It is allocated a lot, in a hot path or in large collections.

```csharp
public readonly record struct OrderId(Guid Value);
public readonly record struct Money(decimal Amount, string Currency);
```

Note `readonly`. A non-readonly record struct has mutable state and gets defensively copied at
call sites, which is slower than the class you were avoiding.

**The sample DTOs in this template are deliberately not structs.** `PingResponse` and
`GreetingResponse` hold a `string` plus a `DateTimeOffset` — around 24 bytes, already past the
guideline — and each one is created once per request and immediately serialized to JSON. The
allocation is invisible next to the serialization. Making them structs would be cargo-culting a
performance idiom into a place where it buys nothing and costs clarity.

There is currently no `readonly record struct` in this repository, because there is no domain layer
and therefore no identifier or value object that warrants one. Do not add one to demonstrate the
feature; add one when you have a value.

## Required members over constructor gymnastics

`required` means the compiler enforces initialisation without you writing a constructor overload for
every combination:

```csharp
public sealed record GreetingRequest
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public required string Name { get; init; }

    [Range(0, 130)]
    public int? Age { get; init; }
}
```

Forget `Name` at a call site and it is a compile error, not a null at runtime.

`init` rather than `set`: the object is immutable once constructed, but object-initialiser syntax and
JSON deserialization both still work.

## Positional or property syntax

Both are records; pick by what the type needs.

**Positional** — `record Thing(string A, int B)` — for simple shapes with nothing to annotate. This
is `PingResponse` and `GreetingResponse`.

**Property syntax** — a body with `{ get; init; }` — when members need attributes, XML docs, or
defaults. This is `GreetingRequest`, because validation attributes have to go somewhere and
attributes on positional parameters need an awkward `[property: Required]` prefix.

Do not mix positional parameters and extra mutable properties in the same record. Pick one.

## The rules that are not negotiable here

**Wire types are `public`.** .NET 10's validation source generator only discovers public types — an
`internal` request record means validation silently never runs and invalid payloads return 200. See
`AGENTS.md`. Everything else in a slice stays `internal`.

**Wire types are `sealed`.** Inheritance and value equality interact badly, and nothing here needs a
DTO hierarchy.

**Every wire type is registered in `ApiJsonSerializerContext`.** Under Native AOT there is no
reflection fallback and a missing entry fails at runtime. See `docs/native-aot.md`.

## What is not a data model

Records are for data. Services, handlers, and anything with behaviour or identity stay classes —
value equality on a thing with dependencies is meaningless and occasionally harmful.

`Greeter` is a static class rather than a record for that reason: it is behaviour with no state.
