using Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.UnitTests.Infrastructure;

/// <summary>
/// The safety net for bugs: nothing exercises it until production does, so it is tested here.
/// </summary>
public class ProblemDetailsExceptionHandlerTests
{
    private static async Task<(bool Handled, int Status, string Body)> Handle(Exception exception)
    {
        var services = new ServiceCollection();
        // The problem details writer needs IOptions, which the web host normally registers.
        services.AddOptions();
        services.AddProblemDetails();
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext
        {
            RequestServices = provider,
            Response = { Body = new MemoryStream() },
        };
        var handler = new ProblemDetailsExceptionHandler(
            provider.GetRequiredService<IProblemDetailsService>(),
            NullLogger<ProblemDetailsExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        return (handled, context.Response.StatusCode, body);
    }

    // Not handling it would fall through to the default: a 500 with an empty body.
    [Fact]
    public async Task Handles_the_exception_as_a_500_with_a_body()
    {
        var (handled, status, body) = await Handle(new InvalidOperationException("boom"));

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.Contains("An unexpected error occurred.", body, StringComparison.Ordinal);
    }

    // Development throws these where production returns the status directly.
    [Fact]
    public async Task Keeps_the_status_of_a_bad_request()
    {
        var (handled, status, body) = await Handle(
            new BadHttpRequestException("Implicit body inferred", StatusCodes.Status400BadRequest));

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.DoesNotContain("Implicit body", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Does_not_leak_the_exception_message()
    {
        var (_, _, body) = await Handle(new InvalidOperationException("connection string user=sa;password=hunter2"));

        Assert.DoesNotContain("hunter2", body, StringComparison.Ordinal);
    }
}
