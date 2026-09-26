using System.Text.Json.Serialization;
using Api.Features.Greetings;
using Api.Features.Ping;
using Microsoft.AspNetCore.Mvc;

namespace Api;

/// <summary>
/// Every type that crosses the wire. Under Native AOT a type missing here fails at runtime, not
/// build. The one shared file each new slice touches.
/// </summary>
[JsonSerializable(typeof(PingResponse))]
[JsonSerializable(typeof(GreetingRequest))]
[JsonSerializable(typeof(GreetingResponse))]
[JsonSerializable(typeof(HttpValidationProblemDetails))]
[JsonSerializable(typeof(ProblemDetails))]
internal sealed partial class ApiJsonSerializerContext : JsonSerializerContext;
