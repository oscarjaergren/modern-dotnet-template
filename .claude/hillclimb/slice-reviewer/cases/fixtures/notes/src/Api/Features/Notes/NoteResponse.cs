namespace Api.Features.Notes;

/// <param name="Id">The note's id.</param>
/// <param name="Title">One-line summary.</param>
/// <param name="Tags">Lower-case labels, sorted.</param>
public sealed record NoteResponse(int Id, string Title, IReadOnlyList<string> Tags);
