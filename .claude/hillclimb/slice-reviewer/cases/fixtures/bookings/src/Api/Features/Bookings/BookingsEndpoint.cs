using Api.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Features.Bookings;

internal static class BookingsEndpoint
{
    internal static IEndpointRouteBuilder MapBookings(this IEndpointRouteBuilder app)
    {
        app.MapPost("/bookings", Handle)
            .WithName("CreateBooking")
            .WithSummary("Books a stay.")
            // Not ProducesProblem(400): it would replace the validation schema, and this one also
            // describes the plain ProblemDetails a past arrival returns.
            .ProducesValidationProblem();

        return app;
    }

    private static Results<Ok<BookingResponse>, ValidationProblem, ProblemHttpResult> Handle(
        BookingRequest request,
        TimeProvider timeProvider)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var booking = BookingDesk.Book(request, today);
        if (booking.IsError)
        {
            return booking.FirstError.ToProblem();
        }

        return TypedResults.Ok(booking.Value);
    }
}
