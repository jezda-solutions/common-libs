using Hangfire;
using Hangfire.PostgreSql;
using Hangfire.AspNetCore;
using Jezda.Common.Abstractions.Configuration.Options;
using Jezda.Common.Extensions.Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System;

namespace Jezda.Common.Extensions;

public static class HangfireExtensions
{
    /// <summary>
    /// Configures the application to use the Hangfire dashboard for job scheduling and monitoring.
    /// </summary>
    /// <remarks>This method integrates the Hangfire dashboard into the application's request pipeline,
    /// allowing users to monitor and manage scheduled jobs. Ensure that the provided <paramref name="serviceProvider"/>
    /// is properly configured to support Hangfire services.</remarks>
    /// <param name="app">The <see cref="IApplicationBuilder"/> instance used to configure the application's request pipeline.</param>
    /// <param name="serviceProvider">The <see cref="IServiceProvider"/> instance used to resolve dependencies required by the Hangfire dashboard.</param>
    /// <param name="url">An optional URL path for accessing the Hangfire dashboard. If <see langword="null"/>, the default URL path is
    /// used.</param>
    /// <returns>The <see cref="IApplicationBuilder"/> instance, enabling method chaining.</returns>
    public static IApplicationBuilder UseScheduling(
        this IApplicationBuilder app,
        IServiceProvider serviceProvider,
        string? url = null)
    {
        // Ensure that the Hangfire dashboard is set up with the provided service provider and URL.
        return app.UseJezdaHangfireDashboard(
            serviceProvider,
            url
        );
    }

    /// <summary>
    /// Configures the Hangfire Dashboard middleware with custom authorization and optional URL settings.
    /// </summary>
    /// <remarks>This method sets up the Hangfire Dashboard with authorization filters based on the
    /// application's configuration. The <paramref name="serviceProvider"/> is used to retrieve the <see
    /// cref="IConfiguration"/> and resolve <see cref="HangfireOptions"/> for customizing the dashboard's
    /// behavior.</remarks>
    /// <param name="app">The <see cref="IApplicationBuilder"/> used to configure the application's request pipeline.</param>
    /// <param name="serviceProvider">The <see cref="IServiceProvider"/> used to resolve application services, including configuration settings.</param>
    /// <param name="url">An optional URL path for the Hangfire Dashboard. Defaults to <c>"/hangfire"</c> if not specified.</param>
    /// <returns>The <see cref="IApplicationBuilder"/> instance, allowing further middleware configuration.</returns>
    public static IApplicationBuilder UseJezdaHangfireDashboard(
        this IApplicationBuilder app,
        IServiceProvider serviceProvider,
        string? url = null)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var hangfireOptions = configuration
            .GetSection(nameof(HangfireOptions))
            .Get<HangfireOptions>() ?? new HangfireOptions();

        var dashboardUrl = url ?? "/hangfire";

        return app.UseHangfireDashboard(
            dashboardUrl,
            new DashboardOptions()
            {
                Authorization = [new HangfireAuthorizationFilter(hangfireOptions)]
            }
        );
    }

    /// <summary>
    /// Configures Hangfire scheduling and server settings for the application.
    /// </summary>
    /// <remarks>This method sets up Hangfire with PostgreSQL storage and configures a Hangfire server to
    /// process jobs from the specified queues. It ensures that the required database is created if it does not already
    /// exist.</remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to which Hangfire services will be added.</param>
    /// <param name="configuration">The application's configuration, used to retrieve the connection string.</param>
    /// <param name="queues">An array of queue names that the Hangfire server will process.</param>
    /// <param name="connection">The name of the connection string to use for Hangfire storage. If <c>null</c>, the default connection string
    /// name "HangfireConnection" will be used.</param>
    /// <returns>The updated <see cref="IServiceCollection"/> with Hangfire services configured.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the specified connection string, or the default "HangfireConnection" connection string, is not
    /// configured.</exception>
    public static IServiceCollection AddJezdaScheduling(
        this IServiceCollection services,
        IConfiguration configuration,
        string[] queues,
        string? connection = null)
    {
        var hangfireConnection = connection ?? "HangfireConnection";

        var connString = configuration.GetConnectionString(hangfireConnection)
            ?? throw new InvalidOperationException($"{hangfireConnection} connection string not configured.");

        CreateDatabase(connString);

        return services
            .AddHangfire((provider, cfg) => cfg
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseActivator(
                    new AspNetCoreJobActivator(
                        provider.GetRequiredService<IServiceScopeFactory>()
                    )
                )
                .UsePostgreSqlStorage(c =>
                    c.UseNpgsqlConnection(connString)
                )
            )
            .AddHangfireServer(options =>
            {
                options.Queues = queues;
            });
    }

    /// <summary>
    /// Creates a new PostgreSQL database if it does not already exist.
    /// </summary>
    /// <remarks>This method connects to the default "postgres" database using the provided connection string
    /// and checks whether the specified database exists. If the database does not exist, it creates the database using
    /// a parameterized query to ensure security.</remarks>
    /// <param name="connectionString">The connection string used to connect to the PostgreSQL server. The connection string must specify the name of
    /// the database to be created in the <c>Database</c> property. If the database name is not specified, an <see
    /// cref="InvalidOperationException"/> is thrown.</param>
    /// <exception cref="InvalidOperationException">Thrown if the <c>Database</c> property in the connection string is null or empty.</exception>
    private static void CreateDatabase(
        string connectionString)
    {
        // Create a connection string builder to manipulate the connection string
        var ncsb = new NpgsqlConnectionStringBuilder(
            connectionString
        );

        var dbName = ncsb.Database;

        // Set the database name to "postgres" to connect to the default database
        ncsb.Database = "postgres";

        // Create a new connection using the modified connection string
        using var connection = new NpgsqlConnection(
            ncsb.ConnectionString
        );

        connection.Open();

        // Check if the database already exists
        using var checkCmd = new NpgsqlCommand(
            "SELECT CASE WHEN EXISTS (SELECT FROM pg_database WHERE datname = @dbname) THEN 0 ELSE 1 END",
            connection
        );

        // If the database name is not specified, throw an exception
        if (string.IsNullOrEmpty(dbName))
        {
            throw new InvalidOperationException(
                "Database name is not specified in the connection string."
            );
        }

        // Use parameterized query to prevent SQL injection
        checkCmd.Parameters.AddWithValue(
            "@dbname", 
            dbName
        );

        // Execute the command to check if the database should be created
        var shouldCreate = Convert.ToInt32(
            checkCmd.ExecuteScalar()
        );

        // If the database does not exist, create it
        if (shouldCreate == 1)
        {
            // Avoid SQL injection by using parameterized queries
            using var createCmd = new NpgsqlCommand(
                $"CREATE DATABASE \"{dbName}\"", // Use double quotes to handle case sensitivity
                connection
            );

            createCmd.ExecuteNonQuery();
        }
    }
}
