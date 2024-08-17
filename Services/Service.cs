using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using System.ServiceProcess;
using System.Diagnostics;

namespace Chetch.Services
{
    abstract public class Service<T>(ILogger<T> logger) : BackgroundService where T : BackgroundService
    {
        public static String ServiceName { get; internal set; } = String.Empty;

        static public IConfigurationRoot GetAppSettings(String filename = "appsettings.json")
        {
            return new ConfigurationBuilder().AddJsonFile(filename).Build();
        }

        static public TraceSource CreateTraceSource(IConfigurationRoot config, String traceSourceName)
        {
            //Tracing
            TraceSource traceSource = null;
            String traceLevel = config.GetValue<String>("Tracing:Level", null);
            if (traceLevel != null && !traceLevel.Equals("None"))
            {
                traceSource = new System.Diagnostics.TraceSource(traceSourceName);
                SourceSwitch sourceSwitch = new SourceSwitch("SourceSwitch", traceLevel);
                traceSource.Switch = sourceSwitch;
                traceSource.Listeners.Add(new System.Diagnostics.ConsoleTraceListener());
            }
            return traceSource;
        }

        static public void Run(String[] args)
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

            var config = GetAppSettings();
            var sourceName = config.GetValue<String>("Logging:EventLog:SourceName");

            if (sourceName == null)
            {
                sourceName = "Chetch";
            }

            ServiceName = sourceName;
            builder.Services.AddWindowsService(options =>
            {
                options.ServiceName = ServiceName;
            });

            if (OperatingSystem.IsWindows())
            {
                LoggerProviderOptions.RegisterProviderOptions<
                    EventLogSettings, EventLogLoggerProvider>(builder.Services);
            }

            builder.Services.AddHostedService<T>();
            
            IHost host = builder.Build();
            host.Run();
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation(10, "Starting service at: {time}", DateTimeOffset.Now);
            return base.StartAsync(cancellationToken);
        }

        abstract protected Task Execute(CancellationToken stoppingToken);
        
        protected void OnError(Exception ex, int eventID = 0)
        {
            logger.LogError(eventID, ex, "Exception: {0}", ex.Message);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                logger.LogInformation(100, "Service started executing at: {time}", DateTimeOffset.Now);

                await Execute(stoppingToken);
                
                logger.LogInformation(100, "Service finished executing at: {time}", DateTimeOffset.Now);
            
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning(100, "Service cancelled at: {time}", DateTimeOffset.Now);
            }
            catch (Exception ex)
            {
                OnError(ex);
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation(1000, "Stopping service at: {time}", DateTimeOffset.Now);
            return base.StopAsync(cancellationToken);
        }
    }
}
