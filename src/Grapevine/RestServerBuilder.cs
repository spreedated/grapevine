using Grapevine.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Grapevine
{
    public class RestServerBuilder
    {
        public IConfiguration Configuration { get; set; }
        public IServiceCollection Services { get; set; }
        public Action<IServiceCollection> ConfigureServices { get; set; }
        public Action<IRestServer> ConfigureServer { get; set; }

        public RestServerBuilder() : this(null, null, null, null) { }

        public RestServerBuilder(IServiceCollection services) : this(services, null, null, null) { }

        public RestServerBuilder(IServiceCollection services, IConfiguration configuration) : this(services, configuration, null, null) { }

        public RestServerBuilder(IServiceCollection services, IConfiguration configuration, Action<IServiceCollection> configureServices) : this(services, configuration, configureServices, null) { }

        public RestServerBuilder(IServiceCollection services, IConfiguration configuration, Action<IRestServer> configureServer) : this(services, configuration, null, configureServer) { }

        public RestServerBuilder(IServiceCollection services, Action<IServiceCollection> configureServices) : this(services, null, configureServices, null) { }

        public RestServerBuilder(IServiceCollection services, Action<IRestServer> configureServer) : this(services, null, null, configureServer) { }

        public RestServerBuilder(IServiceCollection services, Action<IServiceCollection> configureServices, Action<IRestServer> configureServer) : this(services, null, configureServices, configureServer) { }

        public RestServerBuilder(IServiceCollection services, IConfiguration configuration, Action<IServiceCollection> configureServices, Action<IRestServer> configureServer)
        {
            this.Services = services ?? new ServiceCollection();
            this.Configuration = configuration ?? GetDefaultConfiguration();
            this.ConfigureServices = configureServices;
            this.ConfigureServer = configureServer;
        }

        /// <summary>
        /// Build a default server with the specified prefixes
        /// </summary>
        /// <param name="prefixes"></param>
        /// <returns></returns>
        public static IRestServer BuildDefaultServer(params string[] prefixes)
        {
            return BuildDefaultServer(prefixes.Select(Prefix.Parse).ToArray());
        }

        /// <summary>
        /// Build a default server with the specified prefixes
        /// </summary>
        /// <param name="prefixes"></param>
        /// <returns></returns>
        public static IRestServer BuildDefaultServer(params Prefix[] prefixes)
        {
            IRestServer restServer = RestServerBuilder.UseDefaults().Build();

            restServer.Prefixes.Clear();
            foreach (Prefix p in prefixes)
            {
                restServer.Prefixes.Add(p.ToString());
            }

            return restServer;
        }

        public IRestServer Build()
        {
            this.Configuration ??= GetDefaultConfiguration();

            this.Services.AddSingleton(typeof(IConfiguration), this.Configuration);
            this.Services.AddSingleton<IRestServer, RestServer>();
            this.Services.AddSingleton<IRouter, Router>();
            this.Services.AddSingleton<IRouteScanner, RouteScanner>();
            this.Services.AddTransient<IContentFolder, ContentFolder>();

            this.ConfigureServices?.Invoke(this.Services);

            var provider = this.Services.BuildServiceProvider();

            var server = provider.GetRequiredService<IRestServer>();
            server.Router.Services = this.Services;
            server.RouteScanner.Services = this.Services;

            var factory = provider.GetService<ILoggerFactory>();
            if (factory != null) server.SetDefaultLogger(factory);

            var assembly = this.GetType().Assembly.GetName();
            server.GlobalResponseHeaders.Add("Server", $"{assembly.Name}/{assembly.Version} ({RuntimeInformation.OSDescription})");

            // Override with instances
            this.Services.AddSingleton<IRestServer>(server);
            this.Services.AddSingleton<IRouter>(server.Router);
            this.Services.AddSingleton<IRouteScanner>(server.RouteScanner);

            this.ConfigureServer?.Invoke(server);

            return server;
        }

        public static RestServerBuilder UseDefaults()
        {
            var config = GetDefaultConfiguration();

            static void configServices(IServiceCollection services)
            {
                services.AddLogging(configure => configure.AddConsole());
                services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Trace);
            }

            static void configServer(IRestServer server)
            {
                server.Prefixes.Add("http://localhost:1234/");
            }

            return new RestServerBuilder(new ServiceCollection(), config, configServices, configServer);
        }

        public static RestServerBuilder From<T>()
        {
            var type = typeof(T);

            // Get the constructor
            var constructor = Array.Find(type.GetConstructors(), c =>
            {
                var args = c.GetParameters();
                return args.Length == 1 && args[0].ParameterType == typeof(IConfiguration);
            });

            // Get the configuration
            var config = GetDefaultConfiguration();

            // Instanciate startup
            object obj = (constructor != null) ? Activator.CreateInstance(type, config) : Activator.CreateInstance(type);

            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);

            // Initialize Configuration: ReturnType(IConfiguration), Args(null)
            MethodInfo mci = Array.Find(methods, m => m.ReturnParameter.ParameterType == typeof(IConfiguration) && m.GetParameters().Length == 0);
            IConfiguration configInitializer()
            {
                if (mci == null) return config;
                return (IConfiguration)mci.Invoke(obj, null);
            }

            // Initialize Services: ReturnType(IServiceCollection), Args(null)
            MethodInfo msi = Array.Find(methods, m => m.ReturnParameter.ParameterType == typeof(IServiceCollection) && m.GetParameters().Length == 0);
            IServiceCollection serviceInitializer()
            {
                if (msi == null) return new ServiceCollection();
                return (IServiceCollection)msi.Invoke(obj, null);
            }

            // Configure Services: ReturnType(void), Arg[0](IServiceCollection)
            IEnumerable<MethodInfo> mcs = methods.Where(m => m.ReturnParameter.ParameterType == typeof(void) && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(IServiceCollection));
            void configureServices(IServiceCollection s)
            {
                if (!mcs.Any()) return;
                foreach (var method in mcs) method.Invoke(obj, [s]);
            }

            // Configure Server: ReturnType(void), Arg[0](IRestServer)
            IEnumerable<MethodInfo> mcr = methods.Where(m => m.ReturnParameter.ParameterType == typeof(void) && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(IRestServer));
            void configureServer(IRestServer s)
            {
                if (!mcr.Any()) return;
                foreach (var method in mcr) method.Invoke(obj, [s]);
            }

            return new RestServerBuilder(serviceInitializer(), configInitializer(), configureServices, configureServer);
        }

        private static IConfiguration GetDefaultConfiguration()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            return config;
        }
    }

    public static class RestServerBuilderExtensions
    {
        public static RestServerBuilder UseConfiguration(this RestServerBuilder builder, IConfiguration configuration)
        {
            builder.Configuration = configuration;
            return builder;
        }

        public static RestServerBuilder BuildConfiguration(this RestServerBuilder builder, Func<IConfiguration> configurationBuilder)
        {
            builder.Configuration = configurationBuilder.Invoke();
            return builder;
        }

        public static RestServerBuilder UseContainer(this RestServerBuilder builder, IServiceCollection services)
        {
            builder.Services = services;
            return builder;
        }

        public static RestServerBuilder BuildContainer(this RestServerBuilder builder, Func<IServiceCollection> servicesBuilder)
        {
            builder.Services = servicesBuilder.Invoke();
            return builder;
        }
    }
}