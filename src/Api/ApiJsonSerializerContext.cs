using System.Text.Json.Serialization;
using Api.Features.Greetings;
using Api.Features.Ping;
using Microsoft.AspNetCore.Mvc;

namespace Api;

/// <summary>
/// System.Text.Json source-generation context. Under Native AOT there is no reflection fallback:
/// a type that is not listed here fails at runtime, not at build time.
/// </summary>
/// <remarks>
/// When you add a slice, add its request and response types here. This is the one place in the
/// template where slices touch a shared file — see docs/native-aot.md for why source generation
/// is mandatory here.
/// </remarks>
[JsonSerializable(typeof(PingResponse))]
[JsonSerializable(typeof(GreetingRequest))]
[JsonSerializable(typeof(GreetingResponse))]
[JsonSerializable(typeof(HttpValidationProblemDetails))]
[JsonSerializable(typeof(ProblemDetails))]
internal sealed partial class ApiJsonSerializerContext : JsonSerializerContext;
