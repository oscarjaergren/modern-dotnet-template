using System.Collections.Frozen;
using ErrorOr;

namespace Api.Features.Notes;

/// <summary>
/// A fixed, in-memory set of notes: a stand-in for a real store.
/// </summary>
internal static class NoteStore
{
    private static readonly FrozenDictionary<int, (string Title, string[] Tags)> Notes =
        new Dictionary<int, (string, string[])>
        {
            [1] = ("Buy milk", ["home", "Errands"]),
            [2] = ("Renew passport", ["admin"]),
            [3] = ("Call the bank", []),
        }.ToFrozenDictionary();

    internal static ErrorOr<NoteResponse> Find(int id)
    {
        if (!Notes.TryGetValue(id, out var note))
        {
            return Error.NotFound("Notes.NotFound", $"There is no note {id}.");
        }

        var tags = note.Tags.Select(t => t.ToLowerInvariant()).Order(StringComparer.Ordinal).ToArray();
        return new NoteResponse(id, note.Title, tags);
    }
}
