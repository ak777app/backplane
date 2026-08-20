/*
	* SPDX-License-Identifier: Apache-2.0
	* Copyright 2022 FINOS FDC3 contributors - see NOTICE file
	*/

using Finos.Fdc3.Backplane;
using Finos.Fdc3.Backplane.Config;
using Finos.Fdc3.Backplane.Hubs;
using Finos.Fdc3.Backplane.Models.Config;
using Finos.Fdc3.Backplane.MultiHost;
using Finos.Fdc3.Backplane.Utils;
using Finos.Fdc3.Backplane.WorkerService;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net;

/// <summary>
/// Entry point.
/// Create and Host backplane.
/// Only single instance of backplane is enforced. See 'SingleInstance.cs' for more info.
/// </summary>
LoggerFactory _loggerFactory = new LoggerFactory();
SingleInstance _singleInstance = new SingleInstance(_loggerFactory);
ILogger _logger = _loggerFactory.CreateLogger("Program");

if (_singleInstance.IsAlreadyRunning)
{
    _logger.LogCritical("Another instance of backplane service already running. Exiting the current instance..");
    _singleInstance.Dispose();
    Environment.Exit(-10);
}

try
{
    // Setup app settings.
    IConfigurationRoot config = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: false).Build();
    // Read port from app settings.
    PortsConfig portsList = new PortsConfig();
    config.GetSection("PortsConfig").Bind(portsList);

    foreach (int port in portsList.Ports)
    {
        try
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.WebHost.UseKestrel(options => options.ListenAnyIP(port));

            // Add services to the container.
            ConfigureServices(builder.Services, builder.Configuration);

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            ConfigurePipeline(app);

            IHostingUtils hostingUtil = app.Services.GetRequiredService<IHostingUtils>();
            hostingUtil.RegisterBackplane();
            _logger.LogInformation("Backplane service started...");
            await app.RunAsync();
            _logger.LogInformation("Backplane service stopped...");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "An exception occurred while starting the backplane services on port: {Port}", port);
        }
    }
}
catch (Exception exception)
{
    _logger.LogError(exception, "An exception occurred while starting the services.");
    throw;
}
finally
{
    // Dispose once all defined ports are tried or program exiting.
    _singleInstance.Dispose();
}

/// <summary>
/// Configures services for the backplane host.
/// </summary>
static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    services.AddHttpClient("Backplane", (httpConfig) =>
    {
        httpConfig.Timeout = TimeSpan.FromMilliseconds(configuration.GetValue<int>("HttpRequestTimeoutInMilliseconds"));
    });
    services.AddLogging(configure =>
    {
        configure.AddConsole();
        configure.SetMinimumLevel(LogLevel.Information);
    });
    services.AddTransient<IDesktopAgentHub, DesktopAgentsHub>();
    services.AddSignalR(hubOptions =>
    {
        hubOptions.EnableDetailedErrors = true;
    }).AddNewtonsoftJsonProtocol((config) => config.PayloadSerializerSettings = new Newtonsoft.Json.JsonSerializerSettings()
    {
        TypeNameHandling = Newtonsoft.Json.TypeNameHandling.Auto
    });
    services.AddControllers().AddNewtonsoftJson();
    services.AddCors(options =>
    {
        options.AddPolicy("CorsPolicy", corsPolicyBuilder =>
        {
            corsPolicyBuilder.SetIsOriginAllowed((host) => true)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
    });
    // Register services here.
    services.AddSingleton<IHostingUtils, HostingUtils>();
    services.AddSingleton<IConfigRepository, ConfigRepository>();
    services.AddSingleton<INodesRepository, NodesRepository>();
    services.AddSingleton<INodeRegistrationClient, NodeRegistrationClient>();
    services.AddTransient<INodesDiscoveryClient, NodesDiscoveryClient>();
    services.AddHostedService<MemberNodesHealthCheckService>();
}

/// <summary>
/// Configures the HTTP request pipeline.
/// </summary>
static void ConfigurePipeline(WebApplication app)
{
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }

    app.UseExceptionHandler(a => a.Run(async context =>
    {
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        context.Response.ContentType = "application/json";
        IExceptionHandlerFeature? contextFeature = context.Features.Get<IExceptionHandlerFeature>();
        if (contextFeature != null)
        {
            ILogger exLogger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            exLogger.LogError("Unhandled fatal error {Error}", contextFeature.Error);
            await context.Response.WriteAsync(new
            {
                context.Response.StatusCode,
                Message = "Internal Server Error."
            }.ToString() ?? string.Empty);
        }
    }));

    app.UseCors("CorsPolicy");
    app.UseAuthorization();
    app.MapControllers();
    app.MapHub<DesktopAgentsHub>(app.Configuration.GetValue<string>("HubEndPoint") ?? string.Empty);
}

/// <summary>
/// Partial class to allow ILogger&lt;Program&gt; usage with top-level statements.
/// </summary>
public partial class Program { }
