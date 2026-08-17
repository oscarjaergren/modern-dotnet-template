using Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.UnitTests.Infrastructure;

/// <summary>
/// The exception handler is the safety net for bugs, which makes it exactly the kind of code that
/// rots unnoticed — nothing exercises it until production does.
/// </summary>
public class ProblemDetailsExceptionHandlerTests
{
    private static (ProblemDetailsExceptionHandler Handler, DefaultHttpContext Context) Build()
    {
        var services = new ServiceCollection();
        // AddProblemDetails' writer depends on IOptions<ProblemDetailsOptions>, which a bare
        // ServiceCollection does not provide — the web host normally registers it for you.
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

        return (handler, context);
    }

    [Fact]
    public async Task Handles_the_exception()
    {
        var (handler, context) = Build();

        var handled = await handler.TryHandleAsync(
            context, new InvalidOperationException("boom"), TestContext.Current.CancellationToken);

        // Returning false would let the exception fall through to the empty-bodied default 500.
        Assert.True(handled);
    }

    [Fact]
    public async Task Responds_500()
    {
        var (handler, context) = Build();

        await handler.TryHandleAsync(
            context, new InvalidOperationException("boom"), TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }

    [Fact]
    public async Task Writes_a_body_rather_than_an_empty_500()
    {
        var (handler, context) = Build();

        await handler.TryHandleAsync(
            context, new InvalidOperationException("boom"), TestContext.Current.CancellationToken);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);

        Assert.NotEmpty(body);
        Assert.Contains("An unexpected error occurred.", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Does_not_leak_the_exception_message()
    {
        var (handler, context) = Build();

        await handler.TryHandleAsync(
            context,
            new InvalidOperationException("connection string user=sa;password=hunter2"),
            TestContext.Current.CancellationToken);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("hunter2", body, StringComparison.Ordinal);
    }
}
