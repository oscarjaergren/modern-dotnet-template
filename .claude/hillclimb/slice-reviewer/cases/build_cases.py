#!/usr/bin/env python3
"""Generates the slice-reviewer eval cases.

Each case is the template at PINNED with one change on a
`review` branch. Most add the Bookings or Notes slice from fixtures/, with or without a planted
defect; the rest change the existing Greetings slice. For every case this builds the workspace,
regenerates openapi.json (unless the case is about a stale one), runs the tests, and writes:

  generated/<id>.patch   the review branch's diff against main, applied by the runner
  cases.jsonl            id, tags, prompt, label, and the build/test evidence for the label
  ../cases.md            the human review sheet

Usage: build_cases.py [--only id,id] [--skip-tests]
"""
import argparse, json, os, re, shutil, subprocess, sys, tempfile
from pathlib import Path

PINNED = "a342b70"
HERE = Path(__file__).resolve().parent
REPO = Path(subprocess.check_output(["git", "-C", str(HERE), "rev-parse", "--show-toplevel"], text=True).strip())
GEN = HERE / "generated"

BD = "src/Api/Features/Bookings/BookingDesk.cs"
BE = "src/Api/Features/Bookings/BookingsEndpoint.cs"
BQ = "src/Api/Features/Bookings/BookingRequest.cs"
BR = "src/Api/Features/Bookings/BookingResponse.cs"
BU = "tests/Api.UnitTests/Features/Bookings/BookingDeskTests.cs"
BI = "tests/Api.IntegrationTests/Features/Bookings/BookingsEndpointTests.cs"
NS = "src/Api/Features/Notes/NoteStore.cs"
NE = "src/Api/Features/Notes/NotesEndpoint.cs"
NR = "src/Api/Features/Notes/NoteResponse.cs"
NU = "tests/Api.UnitTests/Features/Notes/NoteStoreTests.cs"
NI = "tests/Api.IntegrationTests/Features/Notes/NotesEndpointTests.cs"
GQ = "src/Api/Features/Greetings/GreetingRequest.cs"
GR = "src/Api/Features/Greetings/GreetingResponse.cs"
GE = "src/Api/Features/Greetings/GreetingsEndpoint.cs"
GG = "src/Api/Features/Greetings/Greeter.cs"
GU = "tests/Api.UnitTests/Features/Greetings/GreeterTests.cs"
GI = "tests/Api.IntegrationTests/Features/Greetings/GreetingsEndpointTests.cs"
CTX = "src/Api/ApiJsonSerializerContext.cs"
PROG = "src/Api/Program.cs"
PROPS = "Directory.Packages.props"
API = "src/Api/Api.csproj"


def rep(path, old, new, count=1):
    return ("rep", path, old, new, count)


def write(path, text):
    return ("write", path, text)


def delete(path):
    return ("del", path)


def register(slice_ns, *types):
    """Program.cs and ApiJsonSerializerContext.cs lines for a new slice."""
    ops = [rep(PROG, "using Api.Features.Greetings;\n", f"using Api.Features.Greetings;\nusing Api.Features.{slice_ns};\n"),
           rep(PROG, "app.MapGreetings();\n", f"app.MapGreetings();\napp.Map{slice_ns}();\n"),
           rep(CTX, "using Api.Features.Greetings;\n", f"using Api.Features.Greetings;\nusing Api.Features.{slice_ns};\n")]
    lines = "".join(f"[JsonSerializable(typeof({t}))]\n" for t in types)
    ops.append(rep(CTX, "[JsonSerializable(typeof(HttpValidationProblemDetails))]\n",
                   lines + "[JsonSerializable(typeof(HttpValidationProblemDetails))]\n"))
    return ops


BASES = {
    "bookings": ("Bookings", register("Bookings", "BookingRequest", "BookingResponse")),
    "notes": ("Notes", register("Notes", "NoteResponse")),
    "greetings": ("Greetings", []),
}

# A Notes search endpoint, used by a defect and a clean case.
NOTES_SEARCH = [
    rep(NE, '''            .ProducesProblem(StatusCodes.Status404NotFound);
''', '''            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("/notes/search", Search)
            .WithName("SearchNotes")
            .WithSummary("Finds notes whose title contains the query.");
'''),
    rep(NE, '''        return TypedResults.Ok(note.Value);
    }
''', '''        return TypedResults.Ok(note.Value);
    }

    private static Results<Ok<NoteSearchResponse>, ValidationProblem> Search(NoteSearchRequest request) =>
        TypedResults.Ok(new NoteSearchResponse(NoteStore.Search(request.Query)));
'''),
    rep(NS, '''        return new NoteResponse(id, note.Title, tags);
    }
''', '''        return new NoteResponse(id, note.Title, tags);
    }

    internal static IReadOnlyList<NoteResponse> Search(string query) =>
        [.. Notes.Keys.Order().Where(id => Notes[id].Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(id => Find(id).Value)];
'''),
    write("src/Api/Features/Notes/NoteSearchResponse.cs", '''namespace Api.Features.Notes;

/// <param name="Matches">Notes whose title contains the query, by id.</param>
public sealed record NoteSearchResponse(IReadOnlyList<NoteResponse> Matches);
'''),
    rep(CTX, "[JsonSerializable(typeof(NoteResponse))]\n",
        "[JsonSerializable(typeof(NoteResponse))]\n[JsonSerializable(typeof(NoteSearchRequest))]\n[JsonSerializable(typeof(NoteSearchResponse))]\n"),
    rep(NU, '''    [Fact]
    public void An_unknown_id_is_not_found()''', '''    [Fact]
    public void Search_matches_titles_ignoring_case() =>
        Assert.Equal([2], NoteStore.Search("PASSPORT").Select(n => n.Id));

    [Fact]
    public void An_unknown_id_is_not_found()'''),
    rep(NI, '''    [Fact]
    public async Task Get_note_returns_problem_details_for_an_unknown_id()''', '''    [Fact]
    public async Task Search_notes_returns_matches()
    {
        using var response = await fixture.Client.PostAsJsonAsync(
            "/notes/search", new { query = "bank" }, TestContext.Current.CancellationToken);
        using var body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, body.RootElement.GetProperty("matches")[0].GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Get_note_returns_problem_details_for_an_unknown_id()'''),
    rep(NI, "namespace Api.IntegrationTests.Features.Notes;", "using System.Net.Http.Json;\n\nnamespace Api.IntegrationTests.Features.Notes;"),
]


def search_request(modifier):
    return write("src/Api/Features/Notes/NoteSearchRequest.cs", f'''using System.ComponentModel.DataAnnotations;

namespace Api.Features.Notes;

/// <summary>
/// Request body for <c>POST /notes/search</c>.
/// </summary>
{modifier}sealed record NoteSearchRequest
{{
    /// <summary>Text to find in note titles.</summary>
    [Required]
    [StringLength(40, MinimumLength = 2)]
    public required string Query {{ get; init; }}
}}
''')


SEARCH_400_TEST = rep(NI, '''    [Fact]
    public async Task Get_note_returns_problem_details_for_an_unknown_id()''', '''    [Theory]
    [InlineData("")]
    [InlineData("x")]
    public async Task Search_notes_rejects_a_query_shorter_than_two_characters(string query)
    {
        using var response = await fixture.Client.PostAsJsonAsync(
            "/notes/search", new { query }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_note_returns_problem_details_for_an_unknown_id()''')

# A GET /notes list endpoint, used by a defect and a clean case.
NOTES_LIST = [
    rep(NE, '''            .ProducesProblem(StatusCodes.Status404NotFound);
''', '''            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/notes", () => TypedResults.Ok(NoteStore.All()))
            .WithName("ListNotes")
            .WithSummary("Lists every note.");
'''),
    rep(NS, '''        return new NoteResponse(id, note.Title, tags);
    }
''', '''        return new NoteResponse(id, note.Title, tags);
    }

    internal static NoteResponse[] All() => [.. Notes.Keys.Order().Select(id => Find(id).Value)];
'''),
    rep(NU, '''    [Fact]
    public void An_unknown_id_is_not_found()''', '''    [Fact]
    public void All_lists_every_note_by_id() =>
        Assert.Equal([1, 2, 3], NoteStore.All().Select(n => n.Id));

    [Fact]
    public void An_unknown_id_is_not_found()'''),
    rep(NI, '''    [Fact]
    public async Task Get_note_returns_problem_details_for_an_unknown_id()''', '''    [Fact]
    public async Task List_notes_returns_every_note()
    {
        using var response = await fixture.Client.GetAsync("/notes", TestContext.Current.CancellationToken);
        using var body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, body.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Get_note_returns_problem_details_for_an_unknown_id()'''),
]

# A 409 for fully booked dates, used by a defect and a clean case.
BOOKINGS_CONFLICT = [
    rep(BD, '''        var note = string.IsNullOrWhiteSpace''', '''        if (request.Arrival is { Month: 12, Day: 24 or 25 })
        {
            return Error.Conflict("Bookings.FullyBooked", "The hotel is fully booked over Christmas.");
        }

        var note = string.IsNullOrWhiteSpace'''),
    rep(BU, '''    [Theory]
    [InlineData(null, null)]''', '''    [Fact]
    public void Christmas_is_fully_booked()
    {
        var error = Assert.Single(BookingDesk.Book(Request(new DateOnly(2026, 12, 24)), Today).Errors);

        Assert.Equal(ErrorType.Conflict, error.Type);
    }

    [Theory]
    [InlineData(null, null)]'''),
    rep(BI, '''    [Theory]
    [InlineData("", 1)]''', '''    [Fact]
    public async Task Post_bookings_returns_a_conflict_over_christmas()
    {
        using var response = await Post(new { guestName = "Ada", arrival = "2099-12-24", nights = 1 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("", 1)]'''),
]

GREETINGS_LANGUAGE = [
    rep(GR, '''/// <param name="GreetedAt">Server time when the greeting was composed.</param>
public sealed record GreetingResponse(string Message, DateTimeOffset GreetedAt);''',
        '''/// <param name="GreetedAt">Server time when the greeting was composed.</param>
/// <param name="Language">BCP 47 tag of the greeting's language.</param>
public sealed record GreetingResponse(string Message, DateTimeOffset GreetedAt, string Language);'''),
    rep(GE, "new GreetingResponse(greeting.Value, timeProvider.GetUtcNow())",
        'new GreetingResponse(greeting.Value, timeProvider.GetUtcNow(), "en")'),
    rep(GI, '''        Assert.Equal(expected, body.RootElement.GetProperty("message").GetString());''',
        '''        Assert.Equal(expected, body.RootElement.GetProperty("message").GetString());
        Assert.Equal("en", body.RootElement.GetProperty("language").GetString());'''),
]

PROMPT = "Review the {slice} slice. This branch's changes are in `git diff main`."


def defect(check, summary):
    return {"defect": True, "check": check, "summary": summary}


def clean(why):
    return {"defect": False, "check": None, "summary": why}


CASES = [
    # --- 1. request types are public -----------------------------------------------------------
    dict(id="c1-bookings-internal-request", base="bookings",
         label=defect(1, "BookingRequest is `internal`, so the validation generator skips it and invalid bookings return 200."),
         ops=[rep(BQ, "public sealed record BookingRequest", "internal sealed record BookingRequest")]),
    dict(id="c1-bookings-no-modifier", base="bookings",
         label=defect(1, "BookingRequest has no access modifier, so it is internal and validation is skipped."),
         ops=[rep(BQ, "public sealed record BookingRequest", "sealed record BookingRequest")]),
    dict(id="c1-greetings-made-internal", base="greetings",
         label=defect(1, "GreetingRequest changed to `internal` (and its warning remark removed), so validation is skipped."),
         ops=[rep(GQ, '''/// <remarks>
/// Must be <c>public</c>: the validation source generator skips internal types, and invalid input
/// then returns 200 instead of 400 with no warning anywhere.
/// </remarks>
public sealed record GreetingRequest''', "internal sealed record GreetingRequest")]),
    dict(id="c1-notes-internal-search-request", base="notes",
         label=defect(1, "New NoteSearchRequest is `internal`, so its [StringLength] is never enforced."),
         ops=NOTES_SEARCH + [search_request("internal ")]),

    # --- 2. every wire type is in ApiJsonSerializerContext ------------------------------------
    dict(id="c2-bookings-response-unregistered", base="bookings",
         label=defect(2, "BookingResponse is missing from ApiJsonSerializerContext."),
         ops=[rep(CTX, "[JsonSerializable(typeof(BookingResponse))]\n", "")]),
    dict(id="c2-bookings-request-unregistered", base="bookings",
         label=defect(2, "BookingRequest is missing from ApiJsonSerializerContext."),
         ops=[rep(CTX, "[JsonSerializable(typeof(BookingRequest))]\n", "")]),
    dict(id="c2-notes-response-unregistered", base="notes",
         label=defect(2, "NoteResponse is missing from ApiJsonSerializerContext."),
         ops=[rep(CTX, "[JsonSerializable(typeof(NoteResponse))]\n", ""),
              rep(CTX, "using Api.Features.Notes;\n", "")]),
    dict(id="c2-notes-list-unregistered", base="notes",
         label=defect(2, "GET /notes returns NoteResponse[], which has no ApiJsonSerializerContext entry."),
         ops=NOTES_LIST),

    # --- 3. no reference to another slice -----------------------------------------------------
    dict(id="c3-bookings-using-greeter", base="bookings",
         label=defect(3, "BookingDesk imports Api.Features.Greetings and calls Greeter to reject reserved names."),
         ops=[rep(BD, "using ErrorOr;\n", "using Api.Features.Greetings;\nusing ErrorOr;\n"),
              rep(BD, '''        var note = string.IsNullOrWhiteSpace''', '''        if (Greeter.Greet(request.GuestName, age: null).IsError)
        {
            return Error.Validation("Bookings.ReservedName", "That name cannot hold a booking.");
        }

        var note = string.IsNullOrWhiteSpace'''),
              rep(BU, '''    [Theory]
    [InlineData(null, null)]''', '''    [Fact]
    public void A_reserved_name_cannot_book() =>
        Assert.True(BookingDesk.Book(Request(Today) with { GuestName = "admin" }, Today).IsError);

    [Theory]
    [InlineData(null, null)]''')]),
    dict(id="c3-bookings-qualified-greeter", base="bookings",
         label=defect(3, "BookingsEndpoint calls Api.Features.Greetings.Greeter by its fully qualified name."),
         ops=[rep(BE, "using Api.Infrastructure;\n", "using Api.Infrastructure;\nusing ErrorOr;\n"),
              rep(BE, '''        var today = DateOnly''', '''        if (Api.Features.Greetings.Greeter.Greet(request.GuestName, age: null).IsError)
        {
            return Error.Validation("Bookings.ReservedName", "That name cannot hold a booking.").ToProblem();
        }

        var today = DateOnly''')]),
    dict(id="c3-bookings-embeds-greeting-request", base="bookings",
         label=defect(3, "BookingRequest reuses Api.Features.Greetings.GreetingRequest as its Guest property."),
         ops=[rep(BQ, '''    /// <summary>Name the booking is held under.</summary>
    [Required]
    [StringLength(80, MinimumLength = 1)]
    public required string GuestName { get; init; }''', '''    /// <summary>Who the booking is for; reuses the greeting's name rules.</summary>
    [Required]
    public required Api.Features.Greetings.GreetingRequest Guest { get; init; }'''),
              rep(BD, "            request.GuestName,", "            request.Guest.Name,"),
              rep(BU, 'new() { GuestName = "Ada", Arrival', 'new() { Guest = new() { Name = "Ada" }, Arrival'),
              rep(BI, 'new { guestName = "Ada",', 'new { guest = new { name = "Ada" },', count=4),
              rep(BI, "new { guestName, arrival", "new { guest = new { name = guestName }, arrival")]),
    dict(id="c3-notes-qualified-greeter", base="notes",
         label=defect(3, "NoteStore calls Api.Features.Greetings.Greeter to hide reserved tags."),
         ops=[rep(NS, "var tags = note.Tags.Select(t => t.ToLowerInvariant())",
                  "var tags = note.Tags.Select(t => t.ToLowerInvariant())\n            .Where(t => !Api.Features.Greetings.Greeter.Greet(t, age: null).IsError)")]),

    # --- 4. AOT safety ------------------------------------------------------------------------
    dict(id="c4-notes-reflection-etag", base="notes",
         label=defect(4, "NoteStore.ETag calls reflection-based JsonSerializer.SerializeToUtf8Bytes(note), which Native AOT can't run."),
         ops=[rep(NS, "using System.Collections.Frozen;\n", "using System.Collections.Frozen;\nusing System.Security.Cryptography;\nusing System.Text.Json;\n"),
              rep(NS, '''        return new NoteResponse(id, note.Title, tags);
    }
''', '''        return new NoteResponse(id, note.Title, tags);
    }

    internal static string ETag(NoteResponse note) =>
        $"\\"{Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(note)))[..16]}\\"";
'''),
              rep(NE, "private static Results<Ok<NoteResponse>, ProblemHttpResult> Handle(int id)",
                  "private static Results<Ok<NoteResponse>, ProblemHttpResult> Handle(int id, HttpContext httpContext)"),
              rep(NE, "        return TypedResults.Ok(note.Value);", "        httpContext.Response.Headers.ETag = NoteStore.ETag(note.Value);\n        return TypedResults.Ok(note.Value);")]),
    dict(id="c4-bookings-suppressed-il2026", base="bookings",
         label=defect(4, "BookingDesk.Fingerprint uses reflection-based JsonSerializer and suppresses IL2026/IL3050 with UnconditionalSuppressMessage."),
         ops=[rep(BD, "using ErrorOr;\n", "using System.Diagnostics.CodeAnalysis;\nusing System.Security.Cryptography;\nusing System.Text.Json;\nusing ErrorOr;\n"),
              rep(BD, '$"BK-{request.Arrival:yyyyMMdd}-{request.Nights:D2}"', '$"BK-{request.Arrival:yyyyMMdd}-{request.Nights:D2}-{Fingerprint(request)}"'),
              rep(BD, '''            note);
    }
''', '''            note);
    }

    // A short hash of the request, so two bookings for the same dates get different references.
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "BookingRequest is a plain record.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "BookingRequest is a plain record.")]
    private static string Fingerprint(BookingRequest request) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)))[..6];
'''),
              rep(BU, 'Assert.Equal("BK-20261101-03", booking.Reference);', 'Assert.StartsWith("BK-20261101-03-", booking.Reference, StringComparison.Ordinal);'),
              rep(BI, 'Assert.Equal("BK-20990601-02", body.RootElement.GetProperty("reference").GetString());',
                  'Assert.StartsWith("BK-20990601-02-", body.RootElement.GetProperty("reference").GetString(), StringComparison.Ordinal);')]),
    dict(id="c4-greetings-pragma-echo", base="greetings",
         label=defect(4, "Greeter.Echo calls reflection-based JsonSerializer.Serialize inside `#pragma warning disable IL2026, IL3050`."),
         ops=[rep(GG, "using System.Collections.Frozen;\n", "using System.Collections.Frozen;\nusing System.Text.Json;\n"),
              rep(GG, '''    internal static ErrorOr<string> Greet''', '''    // Echoes the request back, for debugging clients.
#pragma warning disable IL2026, IL3050
    internal static string Echo(GreetingRequest request) => JsonSerializer.Serialize(request);
#pragma warning restore IL2026, IL3050

    internal static ErrorOr<string> Greet'''),
              rep(GR, '''/// <param name="GreetedAt">Server time when the greeting was composed.</param>
public sealed record GreetingResponse(string Message, DateTimeOffset GreetedAt);''',
                  '''/// <param name="GreetedAt">Server time when the greeting was composed.</param>
/// <param name="Echo">The request as the server read it.</param>
public sealed record GreetingResponse(string Message, DateTimeOffset GreetedAt, string Echo);'''),
              rep(GE, "new GreetingResponse(greeting.Value, timeProvider.GetUtcNow())",
                  "new GreetingResponse(greeting.Value, timeProvider.GetUtcNow(), Greeter.Echo(request))")]),
    dict(id="c4-bookings-newtonsoft", base="bookings",
         label=defect(4, "Adds Newtonsoft.Json, which is reflection-based and not AOT-safe, for the reference fingerprint."),
         ops=[rep(PROPS, '''    <PackageVersion Include="Microsoft.AspNetCore.OpenApi"''', '''    <PackageVersion Include="Newtonsoft.Json" Version="13.0.4" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi"'''),
              rep(API, '''    <PackageReference Include="Microsoft.AspNetCore.OpenApi" />''', '''    <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
    <PackageReference Include="Newtonsoft.Json" />'''),
              rep(BD, "using ErrorOr;\n", "using System.Security.Cryptography;\nusing System.Text;\nusing ErrorOr;\nusing Newtonsoft.Json;\n"),
              rep(BD, '$"BK-{request.Arrival:yyyyMMdd}-{request.Nights:D2}"', '$"BK-{request.Arrival:yyyyMMdd}-{request.Nights:D2}-{Fingerprint(request)}"'),
              rep(BD, '''            note);
    }
''', '''            note);
    }

    // A short hash of the request, so two bookings for the same dates get different references.
    private static string Fingerprint(BookingRequest request) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request))))[..6];
'''),
              rep(BU, 'Assert.Equal("BK-20261101-03", booking.Reference);', 'Assert.StartsWith("BK-20261101-03-", booking.Reference, StringComparison.Ordinal);'),
              rep(BI, 'Assert.Equal("BK-20990601-02", body.RootElement.GetProperty("reference").GetString());',
                  'Assert.StartsWith("BK-20990601-02-", body.RootElement.GetProperty("reference").GetString(), StringComparison.Ordinal);')]),

    # --- 5. null-forgiving ! hiding a real nullable flow --------------------------------------
    dict(id="c5-bookings-note-bang", base="bookings",
         label=defect(5, "`request.Note!.Trim()`: Note is optional, so a booking without one throws a NullReferenceException (500)."),
         ops=[rep(BD, "var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();",
                  "var note = request.Note!.Trim();"),
              rep(BU, '''    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("  late check-in ", "late check-in")]
    public void The_note_is_trimmed_and_blank_becomes_null(string? note, string? expected) =>''',
                  '''    [Theory]
    [InlineData("  late check-in ", "late check-in")]
    public void The_note_is_trimmed(string? note, string? expected) =>''')]),
    dict(id="c5-notes-tags-bang", base="notes",
         label=defect(5, "Tags became nullable (note 3 has none) but Find uses `note.Tags!`, so GET /notes/3 throws (500)."),
         ops=[rep(NS, "FrozenDictionary<int, (string Title, string[] Tags)>", "FrozenDictionary<int, (string Title, string[]? Tags)>"),
              rep(NS, "new Dictionary<int, (string, string[])>", "new Dictionary<int, (string, string[]?)>"),
              rep(NS, '[3] = ("Call the bank", []),', '[3] = ("Call the bank", null),'),
              rep(NS, "var tags = note.Tags.Select(", "var tags = note.Tags!.Select("),
              rep(NU, '''    [Fact]
    public void A_note_without_tags_has_an_empty_list() =>
        Assert.Empty(NoteStore.Find(3).Value.Tags);

''', "")]),
    dict(id="c5-bookings-user-agent-bang", base="bookings",
         label=defect(5, "`UserAgent.FirstOrDefault()!`: a request without a User-Agent header throws (500)."),
         ops=[rep(BE, '''        BookingRequest request,
        TimeProvider timeProvider)''', '''        BookingRequest request,
        HttpContext httpContext,
        TimeProvider timeProvider)'''),
              rep(BE, "        return TypedResults.Ok(booking.Value);",
                  "        var channel = httpContext.Request.Headers.UserAgent.FirstOrDefault()!.Split('/')[0];\n        return TypedResults.Ok(booking.Value with { Channel = channel });"),
              rep(BR, '''/// <param name="Note">The front-desk note, trimmed, if one was given.</param>''', '''/// <param name="Note">The front-desk note, trimmed, if one was given.</param>
/// <param name="Channel">The client product that made the booking, from its User-Agent.</param>'''),
              rep(BR, "    string? Note);", "    string? Note,\n    string Channel = \"\");")]),
    dict(id="c5-greetings-nickname-bang", base="greetings",
         label=defect(5, "`Nicknames.GetValueOrDefault(name)!`: a long name with no nickname greets as \"Hello, !\"."),
         ops=[rep(GG, '''    internal static ErrorOr<string> Greet''', '''    // Long names are shortened to a nickname.
    private static readonly FrozenDictionary<string, string> Nicknames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Elizabeth"] = "Liz",
            ["Alexander"] = "Alex",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    internal static ErrorOr<string> Greet'''),
              rep(GG, '''        return age is null''', '''        name = name.Length > 8 ? Nicknames.GetValueOrDefault(name)! : name;

        return age is null'''),
              rep(GU, '''    [InlineData("Grace Hopper")]
''', ""),
              rep(GU, '''    [InlineData("Ada-Lovelace")]
''', "")]),

    # --- 6. tests -----------------------------------------------------------------------------
    dict(id="c6-bookings-unit-tests-misplaced", base="bookings",
         label=defect(6, "BookingDesk's unit tests are at tests/Api.UnitTests/BookingDeskTests.cs, not the mirrored Features/Bookings/ folder."),
         ops=[("move", BU, "tests/Api.UnitTests/BookingDeskTests.cs"),
              rep("tests/Api.UnitTests/BookingDeskTests.cs", "namespace Api.UnitTests.Features.Bookings;", "namespace Api.UnitTests;")]),
    dict(id="c6-bookings-typed-assertions", base="bookings",
         label=defect(6, "The success integration test deserialises into a local C# record instead of asserting the wire format with JsonDocument."),
         ops=[rep(BI, '''        using var response = await Post(new { guestName = "Ada", arrival = FarFuture, nights = 2 });
        using var body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("BK-20990601-02", body.RootElement.GetProperty("reference").GetString());
        Assert.Equal("2099-06-03", body.RootElement.GetProperty("departure").GetString());
    }
''', '''        using var response = await Post(new { guestName = "Ada", arrival = FarFuture, nights = 2 });
        var booking = await response.Content.ReadFromJsonAsync<Booking>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("BK-20990601-02", booking?.Reference);
        Assert.Equal(new DateOnly(2099, 6, 3), booking?.Departure);
    }

    private sealed record Booking(string Reference, DateOnly Departure);
''')]),
    dict(id="c6-bookings-no-400-tests", base="bookings",
         label=defect(6, "No integration test sends invalid input: nothing asserts a 400 for any validated field."),
         ops=[rep(BI, '''    [Theory]
    [InlineData("", 1)]
    [InlineData("Ada", 0)]
    [InlineData("Ada", 31)]
    public async Task Post_bookings_rejects_invalid_input(string guestName, int nights)
    {
        using var response = await Post(new { guestName, arrival = FarFuture, nights });
        using var body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(body.RootElement.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Post_bookings_rejects_a_missing_arrival()
    {
        using var response = await Post(new { guestName = "Ada", nights = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_bookings_rejects_an_overlong_note()
    {
        using var response = await Post(new { guestName = "Ada", arrival = FarFuture, nights = 1, note = new string('x', 201) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
''', "")]),
    dict(id="c6-notes-no-integration-tests", base="notes",
         label=defect(6, "The Notes slice has no integration tests: nothing calls GET /notes/{id} over HTTP."),
         ops=[delete(NI)]),

    # --- 7. contract --------------------------------------------------------------------------
    dict(id="c7-bookings-openapi-not-regenerated", base="bookings", stale_openapi=True,
         label=defect(7, "src/Api/openapi.json was not regenerated: it has no /bookings operation."),
         ops=[]),
    dict(id="c7-greetings-field-openapi-stale", base="greetings", stale_openapi=True,
         label=defect(7, "GreetingResponse gained `Language`, but openapi.json was not regenerated."),
         ops=GREETINGS_LANGUAGE),
    dict(id="c7-bookings-409-undeclared", base="bookings",
         label=defect(7, "BookingDesk can now return a 409, but the endpoint doesn't declare .ProducesProblem(409), so openapi.json lacks it."),
         ops=BOOKINGS_CONFLICT),
    dict(id="c7-notes-route-openapi-stale", base="notes", stale_openapi=True,
         label=defect(7, "The route moved to /notes/{id}/details, but openapi.json still describes /notes/{id}."),
         ops=[rep(NE, 'app.MapGet("/notes/{id:int}", Handle)', 'app.MapGet("/notes/{id:int}/details", Handle)'),
              rep(NI, '"/notes/1"', '"/notes/1/details"'),
              rep(NI, '"/notes/99"', '"/notes/99/details"')]),

    # --- clean: nothing a reviewer should mark Must fix ---------------------------------------
    dict(id="k-bookings-clean", base="bookings",
         label=clean("The Bookings slice as written: public request, registered types, tests for each validated field, regenerated contract."),
         ops=[]),
    dict(id="k-notes-clean", base="notes",
         label=clean("The Notes slice as written."), ops=[]),
    dict(id="k-greetings-wording", base="greetings",
         label=clean("Only the endpoint's OpenAPI description changes, and openapi.json is regenerated."),
         ops=[rep(GE, '.WithDescription("Demonstrates request validation, a typed response, and slice-local logic.")',
                  '.WithDescription("Greets a person by name, and by age when given. Some names are reserved.")')]),
    dict(id="k-bookings-nested-type", base="bookings",
         label=clean("BookingResponse gains a nested StayDetails record. It needs no context entry: the source generator includes property types."),
         ops=[rep(BR, '''/// <param name="Note">The front-desk note, trimmed, if one was given.</param>''', '''/// <param name="Note">The front-desk note, trimmed, if one was given.</param>
/// <param name="Stay">Facts about the stay.</param>'''),
              rep(BR, "    string? Note);", "    string? Note,\n    StayDetails Stay);"),
              write("src/Api/Features/Bookings/StayDetails.cs", '''namespace Api.Features.Bookings;

/// <param name="Nights">Length of the stay.</param>
/// <param name="IncludesWeekend">True when any night is a Friday or Saturday.</param>
public sealed record StayDetails(int Nights, bool IncludesWeekend);
'''),
              rep(BD, '''            note);''', '''            note,
            new StayDetails(
                request.Nights,
                Enumerable.Range(0, request.Nights)
                    .Select(i => request.Arrival.AddDays(i).DayOfWeek)
                    .Any(d => d is DayOfWeek.Friday or DayOfWeek.Saturday)));'''),
              rep(BU, '''    [Fact]
    public void The_reference_encodes_arrival_and_nights()''', '''    [Theory]
    [InlineData(2026, 11, 2, 3, false)]
    [InlineData(2026, 11, 6, 1, true)]
    public void A_stay_includes_the_weekend_when_any_night_is_friday_or_saturday(
        int year, int month, int day, int nights, bool expected)
    {
        var arrival = new DateOnly(year, month, day);

        Assert.Equal(expected, BookingDesk.Book(Request(arrival, nights), Today).Value.Stay.IncludesWeekend);
    }

    [Fact]
    public void The_reference_encodes_arrival_and_nights()'''),
              rep(BI, '''        Assert.Equal("2099-06-03", body.RootElement.GetProperty("departure").GetString());''',
                  '''        Assert.Equal("2099-06-03", body.RootElement.GetProperty("departure").GetString());
        Assert.Equal(2, body.RootElement.GetProperty("stay").GetProperty("nights").GetInt32());''')]),
    dict(id="k-bookings-shared-helper", base="bookings",
         label=clean("Bookings uses a new TimeProvider extension in src/Api/Infrastructure/, shared code outside Features/, which is allowed."),
         ops=[write("src/Api/Infrastructure/TimeProviderExtensions.cs", '''namespace Api.Infrastructure;

internal static class TimeProviderExtensions
{
    /// <summary>Today's date in UTC.</summary>
    internal static DateOnly Today(this TimeProvider timeProvider) =>
        DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
}
'''),
              rep(BE, "        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);\n        var booking = BookingDesk.Book(request, today);",
                  "        var booking = BookingDesk.Book(request, timeProvider.Today());"),
              write("tests/Api.UnitTests/Infrastructure/TimeProviderExtensionsTests.cs", '''using Api.Infrastructure;

namespace Api.UnitTests.Infrastructure;

public class TimeProviderExtensionsTests
{
    [Fact]
    public void Today_is_the_utc_date()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 11, 1, 23, 30, 0, TimeSpan.FromHours(-5)));

        Assert.Equal(new DateOnly(2026, 11, 2), clock.Today());
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
''')]),
    dict(id="k-notes-aot-safe-etag", base="notes",
         label=clean("NoteStore.ETag serialises with ApiJsonSerializerContext.Default.NoteResponse, the AOT-safe overload."),
         ops=[rep(NS, "using System.Collections.Frozen;\n", "using System.Collections.Frozen;\nusing System.Security.Cryptography;\nusing System.Text.Json;\n"),
              rep(NS, '''        return new NoteResponse(id, note.Title, tags);
    }
''', '''        return new NoteResponse(id, note.Title, tags);
    }

    internal static string ETag(NoteResponse note) =>
        $"\\"{Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(note, ApiJsonSerializerContext.Default.NoteResponse)))[..16]}\\"";
'''),
              rep(NE, "private static Results<Ok<NoteResponse>, ProblemHttpResult> Handle(int id)",
                  "private static Results<Ok<NoteResponse>, ProblemHttpResult> Handle(int id, HttpContext httpContext)"),
              rep(NE, "        return TypedResults.Ok(note.Value);", "        httpContext.Response.Headers.ETag = NoteStore.ETag(note.Value);\n        return TypedResults.Ok(note.Value);"),
              rep(NU, '''    [Fact]
    public void An_unknown_id_is_not_found()''', '''    [Fact]
    public void The_etag_is_stable_for_the_same_note() =>
        Assert.Equal(NoteStore.ETag(NoteStore.Find(1).Value), NoteStore.ETag(NoteStore.Find(1).Value));

    [Fact]
    public void An_unknown_id_is_not_found()'''),
              rep(NI, '''        Assert.Equal(2, body.RootElement.GetProperty("tags").GetArrayLength());''',
                  '''        Assert.Equal(2, body.RootElement.GetProperty("tags").GetArrayLength());
        Assert.NotNull(response.Headers.ETag);''')]),
    dict(id="k-bookings-redundant-bang", base="bookings",
         label=clean("`request.Note!.Trim()` after an IsNullOrWhiteSpace check: the ! is redundant, not hiding a null."),
         ops=[rep(BD, "? null : request.Note.Trim();", "? null : request.Note!.Trim();")]),
    dict(id="k-bookings-internal-helper-record", base="bookings",
         label=clean("BookingDesk uses an internal StayPlan record that never crosses the wire; only request types must be public."),
         ops=[rep(BD, '''        var note = string.IsNullOrWhiteSpace''', '''        var plan = new StayPlan(request.Arrival, request.Arrival.AddDays(request.Nights));
        var note = string.IsNullOrWhiteSpace'''),
              rep(BD, '''            request.Arrival,
            request.Arrival.AddDays(request.Nights),''', '''            plan.Arrival,
            plan.Departure,'''),
              rep(BD, '''            note);
    }
}''', '''            note);
    }

    private sealed record StayPlan(DateOnly Arrival, DateOnly Departure);
}''')]),
    dict(id="k-greetings-tests-only", base="greetings",
         label=clean("Adds integration tests for the age boundaries; no production code changes."),
         ops=[rep(GI, '''    // Load-bearing: if validation stops being wired up''', '''    [Theory]
    [InlineData(0)]
    [InlineData(130)]
    public async Task Post_greetings_accepts_the_age_boundaries(int age)
    {
        using var response = await Post(new { name = "Ada", age });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Load-bearing: if validation stops being wired up''')]),
    dict(id="k-greetings-field-regenerated", base="greetings",
         label=clean("GreetingResponse gains `Language`, with openapi.json regenerated and a wire assertion."),
         ops=GREETINGS_LANGUAGE),
    dict(id="k-bookings-409-declared", base="bookings",
         label=clean("BookingDesk can return a 409, the endpoint declares .ProducesProblem(409), and openapi.json has it."),
         ops=BOOKINGS_CONFLICT + [rep(BE, '''            .ProducesValidationProblem();''',
                                       '''            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);''')]),
    dict(id="k-notes-search-public", base="notes",
         label=clean("A public NoteSearchRequest, registered, with tests for the valid and invalid query."),
         ops=NOTES_SEARCH + [search_request("public "), SEARCH_400_TEST]),
]


def sh(*cmd, cwd, check=True, env=None):
    r = subprocess.run(cmd, cwd=cwd, text=True, capture_output=True, env=env)
    if check and r.returncode:
        sys.exit(f"{' '.join(cmd)} failed in {cwd}:\n{r.stdout[-3000:]}\n{r.stderr[-3000:]}")
    return r


def apply(ws, ops, case_id):
    for op in ops:
        kind, path = op[0], ws / op[1]
        if kind == "rep":
            _, _, old, new, count = op
            text = path.read_text()
            if text.count(old) != count:
                sys.exit(f"{case_id}: expected {count} of {old[:60]!r} in {op[1]}, found {text.count(old)}")
            path.write_text(text.replace(old, new))
        elif kind == "write":
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(op[2])
        elif kind == "del":
            path.unlink()
        elif kind == "move":
            dest = ws / op[2]
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.move(path, dest)
        else:
            sys.exit(f"{case_id}: unknown op {kind}")


def failing_tests(output):
    return sorted(set(re.findall(r"^\s*failed ([\w.]+)", output, re.M)))


def build_case(case, skip_tests):
    with tempfile.TemporaryDirectory(prefix=f"sr-{case['id']}-") as tmp:
        ws = Path(tmp)
        sh("bash", "-c", f"git -C {REPO} archive {PINNED} | tar -x -C {ws}", cwd=ws)
        shutil.rmtree(ws / ".claude" / "hillclimb", ignore_errors=True)
        sh("git", "init", "-q", "-b", "main", cwd=ws)
        sh("git", "add", "-A", cwd=ws)
        sh("git", "-c", "user.name=eval", "-c", "user.email=eval@example.invalid", "commit", "-q", "-m", "main", cwd=ws)

        slice_name, base_ops = BASES[case["base"]]
        if case["base"] != "greetings":
            shutil.copytree(HERE / "fixtures" / case["base"], ws, dirs_exist_ok=True)
        apply(ws, base_ops, case["id"])
        apply(ws, case["ops"], case["id"])

        stale = case.get("stale_openapi", False)
        before = (ws / "src/Api/openapi.json").read_bytes()
        env = {**os.environ, "DOTNET_NOLOGO": "1", "DOTNET_CLI_TELEMETRY_OPTOUT": "1"}
        build = sh("dotnet", "build", "--nologo", "-v", "q", cwd=ws, check=False, env=env)
        errors = sorted(set(re.findall(r"error ([A-Z]+\d+)", build.stdout)))
        if stale:
            (ws / "src/Api/openapi.json").write_bytes(before)
        evidence = {"build": "ok" if build.returncode == 0 else "fails", "build_errors": errors}
        if build.returncode == 0 and not skip_tests:
            test = sh("dotnet", "test", cwd=ws, check=False, env=env)
            evidence["tests"] = "pass" if test.returncode == 0 else "fail"
            evidence["failing_tests"] = failing_tests(test.stdout)
        if stale:
            (ws / "src/Api/openapi.json").write_bytes(before)

        sh("bash", "-c", "rm -rf artifacts", cwd=ws)
        sh("git", "add", "-A", cwd=ws)
        patch = sh("git", "diff", "--cached", "--binary", cwd=ws).stdout
        (GEN / f"{case['id']}.patch").write_text(patch)
        return slice_name, evidence


def mutation_diff(case):
    """The case's change relative to its clean base slice: what the reviewer must judge."""
    import difflib
    with tempfile.TemporaryDirectory(prefix="sr-sheet-") as tmp:
        ws = Path(tmp)
        sh("bash", "-c", f"git -C {REPO} archive {PINNED} | tar -x -C {ws}", cwd=ws)
        if case["base"] != "greetings":
            shutil.copytree(HERE / "fixtures" / case["base"], ws, dirs_exist_ok=True)
        apply(ws, BASES[case["base"]][1], case["id"])
        snap = {p.relative_to(ws).as_posix(): p.read_text() for p in ws.rglob("*") if p.is_file() and p.suffix in {".cs", ".csproj", ".props"}}
        apply(ws, case["ops"], case["id"])
        after = {p.relative_to(ws).as_posix(): p.read_text() for p in ws.rglob("*") if p.is_file() and p.suffix in {".cs", ".csproj", ".props"}}
    out = []
    for path in sorted(set(snap) | set(after)):
        a, b = snap.get(path), after.get(path)
        if a != b:
            out += difflib.unified_diff((a or "").splitlines(True), (b or "").splitlines(True),
                                        f"a/{path}" if a is not None else "/dev/null", f"b/{path}" if b is not None else "/dev/null")
    if case.get("stale_openapi"):
        out.append("# src/Api/openapi.json is NOT regenerated in this case.\n")
    return "".join(out)


def review_sheet(rows):
    out = ["# slice-reviewer eval cases", "",
           f"Each case is `{PINNED}` on `main`, and the case's patch on a `review` branch.",
           "The reviewer gets the prompt and the workspace; it never sees the label or this file.", "",
           "| id | slice | expected | check | build | tests |", "|---|---|---|---|---|---|"]
    for r in rows:
        e, lab = r["evidence"], r["label"]
        tests = e.get("tests", "-") + (f" ({', '.join(t.rsplit('.', 1)[-1] for t in e['failing_tests'])})" if e.get("failing_tests") else "")
        out.append(f"| `{r['id']}` | {r['slice']} | {'defect' if lab['defect'] else 'clean'} | {lab['check'] or '-'} | "
                   f"{e['build']}{' ' + ','.join(e['build_errors']) if e['build_errors'] else ''} | {tests} |")
    out += ["", "The two new slices appear in full under `k-bookings-clean` and `k-notes-clean`. Every other case",
            "shows only its change from that clean slice (or from `main`, for Greetings). openapi.json is",
            "regenerated in every case except the three marked stale."]
    by_id = {c["id"]: c for c in CASES}
    for r in rows:
        full = r["id"] in ("k-bookings-clean", "k-notes-clean")
        patch = (GEN / f"{r['id']}.patch").read_text() if full else mutation_diff(by_id[r["id"]])
        if not patch.strip():
            patch = "# (no change: the clean slice as written)\n"
        fence = "`" * max(3, max((len(m) for m in re.findall(r"`+", patch)), default=0) + 1)
        out += ["", f"## `{r['id']}`", "", f"**{'Defect' if r['label']['defect'] else 'Clean'}.** {r['label']['summary']}", "",
                f"Prompt: {r['prompt']}", "", f"{fence}diff", patch.rstrip(), fence]
    return "\n".join(out) + "\n"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only")
    ap.add_argument("--skip-tests", action="store_true")
    ap.add_argument("--sheet-only", action="store_true", help="rewrite cases.md from cases.jsonl without building")
    args = ap.parse_args()
    GEN.mkdir(exist_ok=True)
    if args.sheet_only:
        rows = [json.loads(l) for l in (HERE / "cases.jsonl").read_text().splitlines()]
        (HERE.parent / "cases.md").write_text(review_sheet(rows))
        return
    ids = [c["id"] for c in CASES]
    if len(ids) != len(set(ids)):
        sys.exit("duplicate case id")
    wanted = set(args.only.split(",")) if args.only else None
    old = {}
    if (HERE / "cases.jsonl").exists():
        old = {r["id"]: r for r in map(json.loads, (HERE / "cases.jsonl").read_text().splitlines())}
    rows = []
    for case in CASES:
        if wanted and case["id"] not in wanted:
            if case["id"] in old:
                rows.append(old[case["id"]])
            continue
        slice_name, evidence = build_case(case, args.skip_tests)
        row = {"id": case["id"], "slice": slice_name, "tags": [f"check-{case['label']['check']}" if case["label"]["defect"] else "clean", slice_name.lower()],
               "prompt": PROMPT.format(slice=slice_name), "label": case["label"], "evidence": evidence}
        print(case["id"], json.dumps(evidence), flush=True)
        rows.append(row)
    (HERE / "cases.jsonl").write_text("".join(json.dumps(r) + "\n" for r in rows))
    (HERE.parent / "cases.md").write_text(review_sheet(rows))


if __name__ == "__main__":
    main()
