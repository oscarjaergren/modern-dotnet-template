# Data models

**Read this if:** you are defining a type that mostly holds data: a request, response, value object,
identifier or configuration shape.

## The rule

**Data is a `record`.**

```csharp
public sealed record GreetingResponse(string Message, DateTimeOffset GreetedAt);
```

One line gives value equality, `ToString()`, deconstruction, `with` and immutability; the
hand-written class is forty lines that someone gets wrong, usually `Equals` and `GetHashCode`. Value
equality is also what makes `Assert.Equal(expected, actual)` compare contents.

## Class or struct

Default to `record`. Use `readonly record struct` only when the type is small (about 16 bytes or
less), genuinely a value (an id, a quantity, a unit), and allocated a lot:

```csharp
public readonly record struct OrderId(Guid Value);
```

Keep the `readonly`: a mutable record struct gets defensively copied at call sites, which is slower
than the class you avoided. The sample DTOs are deliberately classes: about 24 bytes each, created
once per request and serialised straight away, so a struct would buy nothing. There is no record
struct in the repo yet, because there is no domain value that needs one. Don't add one to
demonstrate the feature.

## Members

`required` makes a missing member a compile error without constructor overloads; `init` keeps the
object immutable while object initialisers and JSON still work.

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

Use positional syntax for simple shapes (`PingResponse`), and property syntax when members need
attributes, because attributes on positional parameters need `[property: ...]`. Don't mix the two
in one record.

## Non-negotiable here

- **Wire types are `public`.** The validation generator skips internal types, and bad input returns
  200 instead of 400.
- **Wire types are `sealed`.** Inheritance and value equality interact badly.
- **Every wire type is in `ApiJsonSerializerContext`.** Under AOT a missing entry fails at runtime.

## Not data

Services, handlers and anything with behaviour stay classes; value equality on something with
dependencies is meaningless. `Greeter` is a static class because it is behaviour with no state.
