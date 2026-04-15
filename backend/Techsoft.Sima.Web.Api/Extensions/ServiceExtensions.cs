using System.Globalization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;
using Techsoft.Sima.Web.Api.Dtos.Configurations;
using Techsoft.Sima.Web.Api.Helpers;
using Techsoft.Sima.Web.Api.Helpers.HealthChecks;
using Techsoft.Sima.Web.Api.Helpers.Swagger;
using Techsoft.Sima.Web.Application.Services;
using Techsoft.Sima.Web.Domain.Contracts.Repositories;
using Techsoft.Sima.Web.Domain.Services;
using Techsoft.Sima.Web.Infrastructure.Contexts;
using Techsoft.Sima.Web.Infrastructure.Repositories.SolicitudesAcceso;
using Techsoft.Sima.Web.Infrastructure.Repositories.Usuarios;
using Techsoft.Sima.Web.Shared.Logging;
using static Techsoft.Sima.Web.Api.Helpers.ApiConstants;

namespace Techsoft.Sima.Web.Api.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection ConfigureAuthentication(
        this IServiceCollection services,
        GeneralConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = configuration.EntraId.Authority;
                options.Audience = configuration.EntraId.Audience;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    RoleClaimType = "roles"
                };
            });
        return services;
    }

    public static IServiceCollection ConfigureContexts(
        this IServiceCollection services,
        GeneralConfiguration configuration)
    {
        services.AddDbContext<SimaContext>(options =>
            options.UseSqlServer(configuration.DatabaseConnectionString));
        return services;
    }

    public static IServiceCollection ConfigureRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<ISolicitudAccesoRepository, SolicitudAccesoRepository>();
        return services;
    }

    public static IServiceCollection ConfigureDomainServices(this IServiceCollection services)
    {
        services.AddScoped<UsuarioDomainService>();
        services.AddScoped<SolicitudAccesoDomainService>();
        return services;
    }

    public static IServiceCollection ConfigureAppServices(this IServiceCollection services)
    {
        services.AddScoped<UsuarioService>();
        services.AddScoped<SolicitudAccesoService>();
        return services;
    }

    public static IServiceCollection ConfigureApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = VERSION_1;
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = ApiVersionReader.Combine(
                new HeaderApiVersionReader("X-Version"));
        }).AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });
        return services;
    }

    public static IServiceCollection ConfigureSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.DocInclusionPredicate((_, apiDesc) =>
                apiDesc.ActionDescriptor?.EndpointMetadata
                    ?.OfType<SwaggerIgnoreAttribute>().Any() != true);

            options.TagActionsBy(api =>
                api.GroupName != null ? [api.GroupName] : [APP_NAME]);

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Token JWT de Azure Entra ID"
            });

        });

        services.ConfigureOptions<SwaggerConfig>();
        return services;
    }

    public static IServiceCollection ConfigureCors(
        this IServiceCollection services,
        GeneralConfiguration configuration)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(ALLOW_ALL_CORS_POLICY, builder =>
                builder
                    .WithOrigins(configuration.Cors.Origins)
                    .WithExposedHeaders(configuration.Cors.Headers)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials());
        });
        return services;
    }

    public static IServiceCollection ConfigureHealthChecks(
        this IServiceCollection services,
        GeneralConfiguration configuration)
    {
        var version = ASSEMBLY_VERSION;
        services.AddHealthChecks()
            .AddCheck("Liveness", () =>
                Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                    $"API iniciada correctamente. v{version}"))
            .AddCheck("Storage",
                new DatabaseHealthCheck(configuration.DatabaseConnectionString),
                tags: ["ready"]);
        return services;
    }

    public static IServiceCollection ConfigureLogger(
        this IServiceCollection services,
        GeneralConfiguration configuration)
    {
        Log.Logger = new Serilog.LoggerConfiguration()
            .AddConfiguration(configuration)
            .Destructure.ToMaximumDepth(3)
            .CreateLogger();

        services.AddSerilog();
        services.AddTransient(typeof(LoggerWrapper<>));
        return services;
    }

    public static IServiceCollection ConfigureRateLimiter(
        this IServiceCollection services,
        GeneralConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(
                    MetadataName.RetryAfter,
                    out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);
                }
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.WriteAsync(
                    "Demasiados requests. Intente más tarde.", cancellationToken: _);
                return new ValueTask();
            };

            options.AddFixedWindowLimiter(policyName: FIXED_RATE_LIMITING_POLICY, opt =>
            {
                opt.PermitLimit = configuration.RateLimiterMaxCalls;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 0;
            });
        });
        return services;
    }

    private static readonly string[] _supportedLanguages = ["es-MX", "es-GT"];

    public static IServiceCollection ConfigureLocalization(this IServiceCollection services)
    {
        services.AddLocalization();
        var supportedCultures = _supportedLanguages.Select(c => new CultureInfo(c)).ToList();
        services.Configure<RequestLocalizationOptions>(options =>
        {
            options.SupportedCultures = supportedCultures;
            options.SupportedUICultures = supportedCultures;
            options.RequestCultureProviders =
            [
                new Microsoft.AspNetCore.Localization.AcceptLanguageHeaderRequestCultureProvider()
            ];
            options.SetDefaultCulture("es-MX");
        });
        return services;
    }
}
