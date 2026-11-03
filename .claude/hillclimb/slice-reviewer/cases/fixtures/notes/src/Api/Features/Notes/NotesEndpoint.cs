using Api.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Features.Notes;

internal static class NotesEndpoint
{
    internal static IEndpointRouteBuilder MapNotes(this IEndpointRouteBuilder app)
    {
        app.MapGet("/notes/{id:int}", Handle)
            .WithName("GetNote")
            .WithSummary("Gets one note.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static Results<Ok<NoteResponse>, ProblemHttpResult> Handle(int id)
    {
        var note = NoteStore.Find(id);
        if (note.IsError)
        {
            return note.FirstError.ToProblem();
        }

        return TypedResults.Ok(note.Value);
    }
}
