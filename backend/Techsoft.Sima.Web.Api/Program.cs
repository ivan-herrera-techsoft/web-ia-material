using Microsoft.Extensions.Options;
using Serilog;
using Techsoft.Sima.Web.Api.Extensions;
using Techsoft.Sima.Web.Api.Extensions.Configuration;
using Techsoft.Sima.Web.Api.Extensions.Endpoints;
using Techsoft.Sima.Web.Api.Middleware;
using static Techsoft.Sima.Web.Api.Helpers.ApiConstants;

// Log de emergencia en caso de fallo en startup
Log.Logger = new Serilog.LoggerConfiguration().WriteTo.Console().CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    var generalConfiguration = builder.Configuration.GetConfiguration();

    builder.Services
        .ConfigureAuthentication(generalConfiguration)
        .ConfigureApiVersioning()
        .ConfigureSwagger()
        .ConfigureCors(generalConfiguration)
        .ConfigureHealthChecks(generalConfiguration)
        .ConfigureLogger(generalConfiguration)
        .ConfigureContexts(generalConfiguration)
        .ConfigureRepositories()
        .ConfigureDomainServices()
        .ConfigureAppServices()
        .ConfigureRateLimiter(generalConfiguration)
        .ConfigureLocalization()
        .AddAuthorization();

    var app = builder.Build();

    app.UseCors(ALLOW_ALL_CORS_POLICY)
       .UseRateLimiter()
       .UseAuthentication()
       .UseAuthorization()
       .UseRequestLocalization(
           app.Services
              .GetRequiredService<IOptions<RequestLocalizationOptions>>()
              .Value)
       .UseMiddleware<ErrorHandlerMiddleware>();

    if (app.Environment.IsDevelopment())
        app.UseVersionedSwagger();

    app.AddHealthChecks(FIXED_RATE_LIMITING_POLICY)
       .MapHome(FIXED_RATE_LIMITING_POLICY)
       .MapEndpoints(FIXED_RATE_LIMITING_POLICY, generalConfiguration.Cache);

    app.Run();
}
catch (Exception ex)
{
    Log.Logger.Fatal(ex, "Error al iniciar la aplicación {AppName}", APP_NAME);
}
finally
{
    Log.CloseAndFlush();
}
