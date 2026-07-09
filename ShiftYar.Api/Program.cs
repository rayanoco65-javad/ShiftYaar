using Serilog;
using ShiftYar.Infrastructure;
using ShiftYar.API;
using ShiftYar.Application;

var builder = WebApplication.CreateBuilder(args);

// مشخص کردن فایل تنظیمات بر اساس محیط
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

// Configure Serilog
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithThreadId()
    .Enrich.WithEnvironmentName()
);

try
{
    Log.Information("Starting web application");
    Log.Information("Environment: {Environment}", builder.Environment.EnvironmentName);

    // Add services from different layers
    Log.Information("Adding Application services...");
    builder.Services.AddApplication();

    Log.Information("Adding Infrastructure services...");
    builder.Services.AddInfrastructure(builder.Configuration);

    Log.Information("Adding API services...");
    builder.Services.AddAPI(builder.Configuration);

    builder.Services.AddHttpContextAccessor();

    // Add CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll",
            builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader();
            });
    });

    //builder.Services.AddCors(options =>
    //{
    //    options.AddPolicy("AllowSpecificOrigins",
    //        builder =>
    //        {
    //            builder.WithOrigins("http://localhost:3000", "https://yourdomain.com")
    //                   .AllowAnyMethod()
    //                   .AllowAnyHeader();
    //        });
    //});

    var app = builder.Build();
    Log.Information("Application built successfully");

    // Enable Swagger in both Development and Production
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ShiftYar API V1");
        c.RoutePrefix = string.Empty; // swagger در root باز شود
    });
    //if (app.Environment.IsDevelopment())
    //{
    //    Log.Information("Configuring Swagger for Development environment");
    //    app.UseSwagger();
    //    app.UseSwaggerUI(c =>
    //    {
    //        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ShiftYar API V1");
    //        c.RoutePrefix = string.Empty; // swagger در root باز شود
    //    });
    //}

    // Use CORS
    app.UseCors("AllowAll");

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    // Add default route handler
    app.MapGet("/", () => Results.Redirect("/swagger/index.html"));

    Log.Information("Starting the application...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw; // Re-throw to see the full exception in the console
}
finally
{
    Log.CloseAndFlush();
}