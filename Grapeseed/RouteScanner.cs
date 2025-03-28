using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Grapevine
{
    public abstract class RouteScannerBase : IRouteScanner
    {
        public string BasePath { get; set; }

        public IServiceCollection Services { get; set; } = new ServiceCollection();

        /// <summary>
        /// Returns an enumeration of assemblies not on the IgnoredAssemblies list
        /// </summary>
        /// <value></value>
        public IEnumerable<Assembly> Assemblies
        {
            get
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.GetName().Name != "Grapevine" && a.GetName().Name != "Grapeseed" && !a.GetName().Name.StartsWith([.. this.IgnoredAssemblies]))
#if NETSTANDARD
                    .Where(a => !a.GlobalAssemblyCache)
#endif
                    .OrderBy(a => a.FullName)) yield return assembly;
            }
        }

        /// <summary>
        /// List of assembly names to be ignored when scanning all assemblies
        /// </summary>
        /// <value></value>
        public IList<string> IgnoredAssemblies { get; } = new List<string>
        {
            "vshost",
            "xunit",
            "Shouldly",
            "System",
            "Microsoft",
            "netstandard",
            "TestPlatform",
        };

        public abstract IList<IRoute> Scan(string basePath = null);
        public abstract IList<IRoute> Scan(Assembly assembly, string basePath = null);
        public abstract IList<IRoute> Scan(Type type, string basePath = null);
        public abstract IList<IRoute> Scan(MethodInfo methodInfo, string basePath = null);
    }

    public class RouteScanner : RouteScannerBase, IRouteScanner
    {
        /// <summary>
        /// Gets the logger for this RouteScanner object
        /// </summary>
        /// <value></value>
        public ILogger<RouteScanner> Logger { get; }

        public RouteScanner(ILogger<RouteScanner> logger)
        {
            this.Logger = logger ?? DefaultLogger.GetInstance<RouteScanner>();
        }

        public override IList<IRoute> Scan(string basePath = null)
        {
            var basepath = (basePath ?? this.BasePath).SanitizePath();

            List<IRoute> routes = [];
            this.Logger.LogTrace("Begin Global Route Scanning");

            foreach (var assembly in this.Assemblies)
            {
                try
                {
                    routes.AddRange(this.Scan(assembly, basepath));
                }
                catch (ReflectionTypeLoadException ex)
                {
                    var message = $"Exception occurred when scanning for routes";
                    foreach (var loaderEx in ex.LoaderExceptions)
                    {
                        this.Logger.LogDebug(loaderEx, message);
                    }
                }
            }

            this.Logger.LogTrace("Global Route Scanning Complete: {Count} total routes found", routes.Count);

            return routes;
        }

        public override IList<IRoute> Scan(Assembly assembly, string basePath = null)
        {
            var basepath = (basePath ?? this.BasePath).SanitizePath();
            List<IRoute> routes = [];

            var name = assembly.GetName().FullName;
            this.Logger.LogTrace("Scanning assembly {Name} for routes", name);

            try
            {
                foreach (var type in GetQualifiedTypes(assembly))
                    routes.AddRange(this.Scan(type, basepath));
            }
            catch (ReflectionTypeLoadException ex)
            {
                var message = $"Exception occurred when scanning assembly {name} for routes";
                foreach (var loaderEx in ex.LoaderExceptions)
                    this.Logger.LogDebug(loaderEx, message);
            }

            this.Logger.LogTrace("Scan of assembly {Name} complete: {Count} total routes found", name, routes.Count);

            return routes;
        }

        public override IList<IRoute> Scan(Type type, string basePath = null)
        {
            List<IRoute> routes = [];
            this.Logger.LogTrace("Scanning type {Name} for routes", type.Name);

            var attribute = type.GetCustomAttributes(typeof(RestResourceAttribute))
                .Cast<RestResourceAttribute>().FirstOrDefault();

            if (attribute == null)
            {
                return routes;
            }

            var basepath = (basePath ?? this.BasePath).SanitizePath();
            basepath = (!string.IsNullOrWhiteSpace(basepath))
                ? string.Join("/", basepath, attribute.BasePath.SanitizePath())
                : attribute.BasePath.SanitizePath();

            foreach (var method in GetQualifiedMethods(type))
            {
                if (type.IsAbstract && !method.IsStatic) continue;
                routes.AddRange(this.Scan(method, basepath));
            }

            if (routes.Count > 0 && !type.IsAbstract)
            {
                var lifetime = type.IsDefined(typeof(ResourceLifetimeAttribute), false)
                    ? type.GetCustomAttributes(typeof(ResourceLifetimeAttribute))
                        .Cast<ResourceLifetimeAttribute>()
                        .FirstOrDefault()?.ServiceLifetime
                    : ServiceLifetime.Scoped;

                if (lifetime != null)
                {
                    this.Services.TryAdd(new ServiceDescriptor(type, type, (ServiceLifetime)lifetime));
                }
            }

            this.Logger.LogTrace("Scan of type {Name} complete: {Count} total routes found", type.Name, routes.Count);
            return routes;
        }

        public override IList<IRoute> Scan(MethodInfo methodInfo, string basePath = null)
        {
            var basepath = (basePath ?? this.BasePath).SanitizePath();

            List<IRoute> routes = new();
            var type = methodInfo.ReflectedType;

            this.Logger.LogTrace($"Scanning method {methodInfo.Name} for routes");

            var headers = GetAttributes<HeaderAttribute>(methodInfo);

            foreach (var attribute in GetAttributes<RestRouteAttribute>(methodInfo))
            {
                // 1. Create the arguments for Route<T> constructor
                var args = attribute.GenerateRouteConstructorArguments(methodInfo, basepath);

                // 2. Create the Route instance
                var routeType = (!methodInfo.IsStatic)
                    ? typeof(Route<>).MakeGenericType(type)
                    : typeof(Route);
                var route = (IRoute)Activator.CreateInstance(routeType, args);

                // 3. Add any header conditions
                foreach (var header in headers) route.WithHeader(header.Key, header.Value);

                // 4. Add route to routing table
                routes.Add(route);

                this.Logger.LogTrace("Generated route {Route}", route);
            }

            this.Logger.LogTrace("Scan of method {Name} complete: {Count} total routes found", methodInfo.Name, routes.Count);

            return routes;
        }

        /// <summary>
        /// Returns an enumeration of classes in the specified assembly that have the RestResource attribute, order alphabetically by name
        /// </summary>
        /// <param name="assembly"></param>
        /// <returns></returns>
        public static IEnumerable<Type> GetQualifiedTypes(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes()
                .Where(t => t.IsClass && t.IsDefined(typeof(RestResourceAttribute), true))
                .OrderBy(t => t.Name)) yield return type;
        }

        /// <summary>
        /// Returns an enumeration of MethodInfo in the specified type that have the RestRoute attribute, order by appearance in the type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static IEnumerable<MethodInfo> GetQualifiedMethods(Type type)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => !m.IsAbstract && m.IsDefined(typeof(RestRouteAttribute), true))) yield return method;
        }

        /// <summary>
        /// Returns an enumeration of attributes of the specified type
        /// </summary>
        /// <param name="method"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static IEnumerable<T> GetAttributes<T>(MethodInfo method) where T : Attribute
        {
            foreach (var attribute in method.GetCustomAttributes(true)
                .Where(a => a is T)
                .Cast<T>()) yield return attribute;
        }
    }
}