using Api.Features.Notes;
using ErrorOr;

namespace Api.UnitTests.Features.Notes;

public class NoteStoreTests
{
    [Fact]
    public void Tags_are_lower_case_and_sorted() =>
        Assert.Equal(["errands", "home"], NoteStore.Find(1).Value.Tags);

    [Fact]
    public void A_note_without_tags_has_an_empty_list() =>
        Assert.Empty(NoteStore.Find(3).Value.Tags);

    [Fact]
    public void An_unknown_id_is_not_found()
    {
        var error = Assert.Single(NoteStore.Find(99).Errors);

        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal("Notes.NotFound", error.Code);
    }
}
