var builder = DistributedApplication.CreateBuilder(args);

var basketAPI = builder.AddProject<Projects.otel_Basket_API>("basket.api");
var catalogAPI = builder.AddProject<Projects.otel_Catalog_API>("catalog.api");

builder.Build().Run();