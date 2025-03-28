using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Grapevine
{
    /// <summary>
    /// Delegate for <see cref="Router.GlobalErrorHandlers"/> and <see cref="Router.LocalErrorHandlers"/>
    /// </summary>
    /// <param name="context"></param>
    /// <param name="e"></param>
    /// <returns></returns>
    public delegate Task HandleErrorAsync(IHttpContext context, Exception e);

    public abstract class RouterBase : IRouter
    {
        /// <summary>
        /// Gets or sets the default routing error handler
        /// </summary>
        /// <returns></returns>
        public static HandleErrorAsync DefaultErrorHandler { get; set; } = async (context, exception) =>
        {
            if (context == null)
            {
                return;
            }

            string content = context?.Response?.StatusCode.ToString() ?? HttpStatusCode.InternalServerError.ToString();

            if (exception != null)
            {
                content = $"Internal Server Error: {exception.Message}";
            }
            else if (context.Response.StatusCode == HttpStatusCode.NotFound)
            {
                content = $"File Not Found: {context.Request.Endpoint}";
            }
            else if (context.Response.StatusCode == HttpStatusCode.NotImplemented)
            {
                content = $"Method Not Implemented: {context.Request.Name}";
            }

            await context.Response.SendResponseAsync(content);
        };

        /// <summary>
        /// Collection of global error handlers keyed by HTTP status code
        /// </summary>
        /// <typeparam name="HttpStatusCode"></typeparam>
        /// <typeparam name="HandleErrorAsync"></typeparam>
        /// <returns></returns>
        public static Dictionary<HttpStatusCode, HandleErrorAsync> GlobalErrorHandlers { get; } = new();

        /// <summary>
        /// Collection of error handlers specific to this Router object
        /// </summary>
        /// <typeparam name="HttpStatusCode"></typeparam>
        /// <typeparam name="HandleErrorAsync"></typeparam>
        /// <returns></returns>
        public Dictionary<HttpStatusCode, HandleErrorAsync> LocalErrorHandlers { get; } = new();

        public virtual string Id { get; } = Guid.NewGuid().ToString();

        public RouterOptions Options { get; } = new();

        public abstract IList<IRoute> RoutingTable { get; }

        public IServiceCollection Services { get; set; }

        public IServiceProvider ServiceProvider { get; set; }

        public virtual RequestRoutingEvent AfterRoutingAsync { get; set; } = new();
        public virtual RequestRoutingEvent BeforeRoutingAsync { get; set; } = new();

        public abstract IRouter Register(IRoute route);

        public abstract void RouteAsync(object state);

        /// <summary>
        /// Asychronously determines which error handler to invoke and invokes with the given context and exception
        /// </summary>
        /// <param name="context"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        protected internal async Task HandleErrorAsync(IHttpContext context, Exception e = null)
        {
            if (context.WasRespondedTo) return;

            if (context.Response.StatusCode == HttpStatusCode.Ok)
                context.Response.StatusCode = HttpStatusCode.InternalServerError;

            if (!this.LocalErrorHandlers.ContainsKey(context.Response.StatusCode))
            {
                this.LocalErrorHandlers[context.Response.StatusCode] = GlobalErrorHandlers.ContainsKey(context.Response.StatusCode)
                    ? GlobalErrorHandlers[context.Response.StatusCode]
                    : DefaultErrorHandler;
            }

            var action = this.LocalErrorHandlers[context.Response.StatusCode];

            try
            {
                await action(context, (this.Options.SendExceptionMessages) ? e : null).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.Print(ex.Message);
            }
        }
    }

    public class Router : RouterBase
    {
        /// <summary>
        /// Gets the logger for this Router object.
        /// </summary>
        /// <value></value>
        public ILogger<IRouter> Logger { get; }

        /// <summary>
        /// List of all registered routes.
        /// </summary>
        /// <typeparam name="IRoute"></typeparam>
        /// <returns></returns>
        protected internal readonly IList<IRoute> RegisteredRoutes = new List<IRoute>();

        public override IList<IRoute> RoutingTable => this.RegisteredRoutes.ToList().AsReadOnly();

        public Router(ILogger<IRouter> logger)
        {
            this.Logger = logger ?? DefaultLogger.GetInstance<IRouter>();
        }

        public override IRouter Register(IRoute route)
        {
            if (this.RegisteredRoutes.All(r => !route.Equals(r))) this.RegisteredRoutes.Add(route);
            return this;
        }

        public override async void RouteAsync(object state)
        {
            if (state is not IHttpContext context) return;

            try
            {
                context.Response.ContentExpiresDuration = this.Options.ContentExpiresDuration;

                this.Logger.LogDebug($"{context.Id} : Routing {context.Request.Name}");

                var routesExecuted = await this.RouteAsync(context);
                if (routesExecuted == 0 || ((context.Response.StatusCode != HttpStatusCode.Ok || this.Options.RequireRouteResponse) && !context.WasRespondedTo))
                {
                    if (context.Response.StatusCode == HttpStatusCode.Ok)
                        context.Response.StatusCode = HttpStatusCode.NotImplemented;
                    await this.HandleErrorAsync(context);
                }
            }
            catch (Exception e)
            {
                this.Logger.LogError(e, $"{context.Id}: An exception occurred while routing request {context.Request.Name}");
                await this.HandleErrorAsync(context, e);
            }
        }

        /// <summary>
        /// Routes the IHttpContext through matching routes
        /// </summary>
        /// <param name="context"></param>
        public virtual async Task<int> RouteAsync(IHttpContext context)
        {
            // 0. If no routes are found, there is nothing to do here
            var routing = this.RoutesFor(context);
            if (!routing.Any()) return 0;
            this.Logger.LogDebug($"{context.Id} : Matching routes discovered for {context.Request.Name}");

            // 1. Create a scoped container for dependency injection
            this.ServiceProvider ??= this.Services.BuildServiceProvider();
            context.Services = this.ServiceProvider.CreateScope().ServiceProvider;

            // 2. Invoke before routing handlers
            if (!context.CancellationToken.IsCancellationRequested)
            {
                this.Logger.LogTrace($"{context.Id} : Invoking before routing handlers for {context.Request.Name}");
                var beforeCount = (this.BeforeRoutingAsync != null) ? await this.BeforeRoutingAsync.Invoke(context) : 0;
                this.Logger.LogTrace($"{context.Id} : {beforeCount} Before routing handlers invoked for {context.Request.Name}");
            }

            // 3. Iterate over the routes until a response is sent
            var count = 0;
            foreach (var route in routing)
            {
                if (context.CancellationToken.IsCancellationRequested) break;
                if (context.Response.StatusCode != HttpStatusCode.Ok) break;
                if (context.WasRespondedTo && !this.Options.ContinueRoutingAfterResponseSent) break;
                this.Logger.LogDebug($"{context.Id} : Executing {route.Name} for {context.Request.Name}");
                await route.InvokeAsync(context);
                count++;
            }
            this.Logger.LogDebug($"{context.Id} : {count} of {routing.Count()} routes invoked");

            // 4. Invoke after routing handlers
            if (!context.CancellationToken.IsCancellationRequested)
            {
                this.Logger.LogTrace($"{context.Id} : Invoking after routing handlers for {context.Request.Name}");
                var afterCount = (this.AfterRoutingAsync != null) ? await this.AfterRoutingAsync.Invoke(context) : 0;
                this.Logger.LogTrace($"{context.Id} : {afterCount} After routing handlers invoked for {context.Request.Name}");
            }

            return count;
        }

        /// <summary>
        /// Gets an enumeration of registered routes that match the IHttpContext provided
        /// </summary>
        /// <param name="context"></param>
        /// <returns>IEnumerable&lt;IRoute&gt;</returns>
        public virtual IEnumerable<IRoute> RoutesFor(IHttpContext context)
        {
            foreach (var route in this.RegisteredRoutes.Where(r => r.IsMatch(context) && r.Enabled)) yield return route;
        }
    }
}