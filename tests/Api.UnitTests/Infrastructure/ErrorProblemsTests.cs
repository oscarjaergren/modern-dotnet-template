using Api.Infrastructure;
using ErrorOr;

namespace Api.UnitTests.Infrastructure;

public class ErrorProblemsTests
{
    public static TheoryData<Error, int> Errors => new()
    {
        { Error.Validation(description: "d"), 400 },
        { Error.Unauthorized(description: "d"), 401 },
        { Error.Forbidden(description: "d"), 403 },
        { Error.NotFound(description: "d"), 404 },
        { Error.Conflict(description: "d"), 409 },
        { Error.Failure(description: "d"), 500 },
        { Error.Unexpected(description: "d"), 500 },
    };

    [Theory]
    [MemberData(nameof(Errors))]
    public void Each_error_type_maps_to_its_status_with_the_description_as_detail(
        Error error, int status)
    {
        var problem = error.ToProblem();

        Assert.Equal(status, problem.StatusCode);
        Assert.Equal("d", problem.ProblemDetails.Detail);
    }
}
