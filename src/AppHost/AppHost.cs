var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Api>("api")
    .WithHttpHealthCheck("/health");

builder.Build().Run();
