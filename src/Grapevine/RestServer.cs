using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading;

namespace Grapevine
{
    public class RestServer : RestServerBase
    {
        /// <summary>
        /// The thread that listens for incoming requests.
        /// </summary>
        public readonly Thread RequestHandler;

        /// <summary>
        /// Gets a value that indicates whether the object has been disposed.
        /// </summary>
        /// <value></value>
        public bool IsDisposed { get; private set; }

        public override bool IsListening => Convert.ToBoolean(this.Listener?.IsListening);

        /// <summary>
        /// Gets a value that indicates whether the server is in the process of stopping.
        /// </summary>
        /// <value></value>
        public bool IsStopping { get; protected set; }

        /// <summary>
        /// Gets a value that indicates whether the server is in the process of starting.
        /// </summary>
        /// <value></value>
        public bool IsStarting { get; protected set; }

        /// <summary>
        /// Gets the HttpListener object used by this RestServer object.
        /// </summary>
        /// <value></value>
        public HttpListener Listener { get; protected internal set; }

        public override IListenerPrefixCollection Prefixes { get; }

        public override event ServerEventHandler AfterStarting;
        public override event ServerEventHandler AfterStopping;
        public override event ServerEventHandler BeforeStarting;
        public override event ServerEventHandler BeforeStopping;

        public RestServer(IRouter router, IRouteScanner scanner, ILogger<IRestServer> logger)
        {
            if (!HttpListener.IsSupported)
            {
                throw new PlatformNotSupportedException("Windows Server 2003 (or higher) or Windows XP SP2 (or higher) is required to use instances of this class.");
            }

            this.Router = router ?? new Router(DefaultLogger.GetInstance<IRouter>());
            this.RouteScanner = scanner ?? new RouteScanner(DefaultLogger.GetInstance<IRouteScanner>());
            this.Logger = logger ?? DefaultLogger.GetInstance<IRestServer>();

            if (this.Router is RouterBase routerBase)
            {
                routerBase.HandleHttpListenerExceptions();
            }

            this.RouteScanner.Services = this.Router.Services;

            this.Listener = new HttpListener();
            this.Prefixes = new ListenerPrefixCollection(this.Listener.Prefixes);
            this.RequestHandler = new Thread(this.RequestListenerAsync);
        }

        public override void Start()
        {
            if (this.IsDisposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            if (this.IsListening || this.IsStarting || this.IsStopping)
            {
                return;
            }

            this.IsStarting = true;

            var exceptionWasThrown = false;

            try
            {
                // 1. Reset CancellationTokenSource
                this.TokenSource?.Dispose();
                this.TokenSource = new CancellationTokenSource();

                // 2. Execute BeforeStarting event handlers
                BeforeStarting?.Invoke(this);

                // 3. Optionally autoscan for routes
                if (this.Router.RoutingTable.Count == 0 && this.Options.EnableAutoScan)
                {
                    this.Router.Register(this.RouteScanner.Scan());
                }

                // 4. Configure and start the listener
                this.Listener.Start();

                // 5. Start the request handler thread
                this.RequestHandler.Start();

                // 6. Execute AfterStarting event handlers
                AfterStarting?.Invoke(this);
            }
            catch (HttpListenerException hl) when (hl.ErrorCode == 32)
            {
                /*
                * When the port you are attempting to bind to is already in use
                * by another application, the error can sometimes be unintuitive.
                */

                exceptionWasThrown = true;

                var message = $"One or more ports are already in use by another application.";
                var exception = new ArgumentException(message, hl);

                this.Logger.LogCritical(exception, message);
                throw exception;
            }
            catch (Exception e)
            {
                exceptionWasThrown = true;

                this.Logger.LogCritical(e, "An unexpected error occurred when attempting to start the server");
                throw;
            }
            finally
            {
                if (exceptionWasThrown)
                {
                    this.Listener.Stop();
                    this.TokenSource?.Cancel();
                }

                this.IsStarting = false;

                if (this.ContentFolders.Count > 0)
                {
                    this.Logger.LogInformation("Grapevine has detected that content folders have been added");
                    this.Logger.LogInformation("Enable serving content from content folders with: server.UseContentFolders()");
                }
            }
        }

        public override void Stop()
        {
            if (this.IsDisposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            if (this.IsStopping || this.IsStarting)
            {
                return;
            }

            this.IsStopping = true;

            try
            {
                // 1. Execute BeforeStopping event handlers
                BeforeStopping?.Invoke(this);

                // 2. Stop the listener
                this.Listener?.Stop();

                // 3. Complete or cancel running routes
                this.TokenSource?.Cancel();

                // 4. Execute AfterStopping event handlers
                AfterStopping?.Invoke(this);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Stopping error");
                throw;
            }
            finally
            {
                this.IsStopping = false;
            }
        }

        protected void RequestListenerAsync()
        {
            while (this.Listener.IsListening)
            {
                try
                {
                    HttpListenerContext context = this.Listener.GetContextAsync().Result;
                    ThreadPool.QueueUserWorkItem(this.RequestHandlerAsync, context);
                }
                catch (HttpListenerException hl) when (hl.ErrorCode == 995 && (this.IsStopping || !this.IsListening))
                {
                    //noop
                }
                catch (ObjectDisposedException) when (this.IsDisposed)
                {
                    //noop
                }
                catch (Exception e)
                {
                    this.Logger.LogDebug(e, "An unexpected error occurred while listening for incoming requests.");
                }
            }
        }

        protected void RequestHandlerAsync(object state)
        {
            // 1. Create context
            IHttpContext context = this.Options.HttpContextFactory(state, this.TokenSource.Token);
            this.Logger.LogTrace("{Id} : Request Received {Name}", context.Id, context.Request.Name);

            // 2. Apply global response headers
            this.ApplyGlobalResponseHeaders(context.Response.Headers);

            // 3. Execute OnRequest event handlers
            try
            {
                this.Logger.LogTrace("{Id} : Invoking OnRequest Handlers for {Name}", context.Id, context.Request.Name);
                var count = (this.OnRequestAsync != null) ? this.OnRequestAsync.Invoke(context, this).Result : 0;
                this.Logger.LogTrace("{Id} : {Count} OnRequest Handlers Invoked for {Name}", context.Id, count, context.Request.Name);
            }
            catch (HttpListenerException hl) when (hl.ErrorCode == 1229)
            {
                this.Logger.LogError(hl, "{Id} : The remote connection was closed before a response could be sent for {Name}.", context.Id, context.Request.Name);
            }
            catch (Exception e)
            {
                this.Logger.LogError(e, "{Id} An exception occurred while routing request {Name}", context.Id, context.Request.Name);
            }

            // 4. Optionally route request
            if (!context.WasRespondedTo)
            {
                this.Logger.LogTrace("{Id} : Routing request {Name}", context.Id, context.Request.Name);
                ThreadPool.QueueUserWorkItem(this.Router.RouteAsync, context);
            }
        }

        #region Dispose
        public override void Dispose()
        {
            if (this.IsDisposed)
            {
                return;
            }

            try
            {
                this.Stop();
                this.Listener.Close();
                this.TokenSource?.Dispose();
            }
            finally
            {
                this.IsDisposed = true;
            }
        }
        #endregion
    }
}