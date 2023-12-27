var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.otel_Basket_API>("basket.api");

builder.Build().Run();