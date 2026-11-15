// File: DependencyInjection.cs
// Purpose: One AddInfrastructure() extension method registers AppDbContext,
// Identity, S3StorageService, RabbitMqPublisher, and everything else this
// layer owns. Both Api/Program.cs and Worker/Program.cs call this exact
// same method, so both processes get identically configured infrastructure
// (ARCHITECTURE.md section 2.3).
namespace VideoPlatform.Infrastructure;

using Amazon;
using Amazon.S3;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VideoPlatform.Application.Common.Interfaces;
using VideoPlatform.Infrastructure.Auth;
using VideoPlatform.Infrastructure.Identity;
using VideoPlatform.Infrastructure.Messaging;
using VideoPlatform.Infrastructure.Persistence;
using VideoPlatform.Infrastructure.Services;
using VideoPlatform.Infrastructure.Storage;

public static class DependencyInjection
{
    // registerConsumers / configureReceiveEndpoints: the Api only ever sends
    // messages, so it passes neither. The Worker passes both — the first
    // registers its consumer classes, the second declares the receive
    // endpoints (queue names, prefetch, concurrency) they listen on.
    // When configureReceiveEndpoints is omitted, MassTransit's default
    // ConfigureEndpoints convention names a queue per registered consumer.
    // Keeping the RabbitMQ *bus* wiring (host, retry policy) in one shared
    // method avoids duplicating it between the two processes.
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? registerConsumers = null,
        Action<IRabbitMqBusFactoryConfigurator, IBusRegistrationContext>? configureReceiveEndpoints = null)
    {
        // EnableRetryOnFailure makes short database hiccups (a restarting
        // container, a dropped connection) invisible instead of a failed request.
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.EnableRetryOnFailure()));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // ASP.NET Core Identity owns password hashing and account security
        // end-to-end (TOOLS.md, "ASP.NET Core Identity") — Application and
        // Domain never see a password in any form. Only the user/role stores
        // and UserManager are registered: sign-in is verified in
        // AuthEndpoints (UserManager + lockout), so no cookie-oriented
        // SignInManager or token providers are needed — which also keeps the
        // Worker, which shares this method, free of web-only services.
        services
            .AddIdentityCore<ApplicationUser>(identityOptions =>
            {
                identityOptions.Password.RequiredLength = 8;
                identityOptions.Password.RequireDigit = true;
                identityOptions.Password.RequireLowercase = true;
                identityOptions.Password.RequireUppercase = true;
                identityOptions.Password.RequireNonAlphanumeric = false;
                identityOptions.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();

        // Presigned browser PUTs must use SigV4 even in us-east-1.
        AWSConfigsS3.UseSignatureVersion4 = true;
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddSingleton<IAmazonS3>(_ =>
        {
            var storageOptions = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>()
                                  ?? new StorageOptions();

            return S3ClientFactory.Create(storageOptions);
        });
        services.AddSingleton<IStorageService, S3StorageService>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.AddScoped<IVideoProcessingPublisher, RabbitMqPublisher>();

        services.AddMassTransit(bus =>
        {
            registerConsumers?.Invoke(bus);

            bus.UsingRabbitMq((context, cfg) =>
            {
                var rabbitSection = configuration.GetSection("RabbitMq");

                cfg.Host(rabbitSection["Host"] ?? "localhost", rabbitSection["VirtualHost"] ?? "/", host =>
                {
                    host.Username(rabbitSection["Username"] ?? "guest");
                    host.Password(rabbitSection["Password"] ?? "guest");
                });

                // Three retries (after 5 s, 30 s, 2 min) before MassTransit
                // gives up, publishes Fault<T> and parks the message in the
                // "<queue>_error" queue — the concrete "retries with backoff,
                // then dead-letter" behavior FLOW.md's Video Processing Flow
                // step 7 describes. It is not MassTransit's default (zero
                // retries), so it is set explicitly here. The growing delays
                // let a short S3 / database outage heal between attempts.
                cfg.UseMessageRetry(retry => retry.Intervals(
                    TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2)));

                if (configureReceiveEndpoints is null)
                    cfg.ConfigureEndpoints(context);
                else
                    configureReceiveEndpoints(cfg, context);
            });
        });

        return services;
    }
}
