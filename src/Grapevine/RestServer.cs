using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace Grapevine
{
    public abstract class RestServerBase : IRestServer
    {
        public IList<IContentFolder> ContentFolders { get; } = new List<IContentFolder>();

        public IList<GlobalResponseHeaders> GlobalResponseHeaders { get; set; } = new List<GlobalResponseHeaders>();

        public virtual bool IsListening { get; }

        public Locals Locals { get; set; } = new Locals();

        public ILogger<IRestServer> Logger { get; protected set; }

        public ServerOptions Options { get; } = new ServerOptions
        {
            HttpContextFactory = (state, token) => new HttpContext(state as HttpListenerContext, token)
        };

        public virtual IListenerPrefixCollection Prefixes { get; }

        public IRouter Router { get; set; }

        public IRouteScanner RouteScanner { get; set; }

        /// <summary>
        /// Gets or sets the CancellationTokeSource for this RestServer object.
        /// </summary>
        /// <value></value>
        protected CancellationTokenSource TokenSource { get; set; }

        public abstract event ServerEventHandler AfterStarting;
        public abstract event ServerEventHandler AfterStopping;
        public abstract event ServerEventHandler BeforeStarting;
        public abstract event ServerEventHandler BeforeStopping;
        public virtual RequestReceivedEvent OnRequestAsync { get; set; } = new RequestReceivedEvent();

        public abstract void Dispose();

        public abstract void Start();

        public abstract void Stop();
    }

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

        public override bool IsListening => Convert.ToBoolean(Listener?.IsListening);

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
                throw new PlatformNotSupportedException("Windows Server 2003 (or higher) or Windows XP SP2 (or higher) is required to use instances of this class.");

            this.Router = router ?? new Router(DefaultLogger.GetInstance<IRouter>());
            this.RouteScanner = scanner ?? new RouteScanner(DefaultLogger.GetInstance<IRouteScanner>());
            this.Logger = logger ?? DefaultLogger.GetInstance<IRestServer>();

            if (this.Router is RouterBase)
                (this.Router as RouterBase).HandleHttpListenerExceptions();

            this.RouteScanner.Services = this.Router.Services;

            this.Listener = new HttpListener();
            this.Prefixes = new ListenerPrefixCollection(this.Listener.Prefixes);
            this.RequestHandler = new Thread(this.RequestListenerAsync);
        }

        public override void Dispose()
        {
            if (this.IsDisposed) return;

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

        public override void Start()
        {
            if (this.IsDisposed) throw new ObjectDisposedException(this.GetType().FullName);
            if (this.IsListening || this.IsStarting || this.IsStopping) return;
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
                    this.Router.Register(this.RouteScanner.Scan());

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
                    this.TokenSource.Cancel();
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
            if (this.IsDisposed) throw new ObjectDisposedException(this.GetType().FullName);
            if (this.IsStopping || this.IsStarting) return;
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

        protected async void RequestListenerAsync()
        {
            while (this.Listener.IsListening)
            {
                try
                {
                    var context = await this.Listener.GetContextAsync();
                    ThreadPool.QueueUserWorkItem(this.RequestHandlerAsync, context);
                }
                catch (HttpListenerException hl) when (hl.ErrorCode == 995 && (IsStopping || !IsListening))
                {
                    /*
                    * Ignore exceptions thrown by incomplete async methods listening for
                    * incoming requests during shutdown
                    */
                }
                catch (ObjectDisposedException) when (this.IsDisposed)
                {
                    /*
                    * Ignore object disposed exceptions thrown during shutdown
                    * see: https://stackoverflow.com/a/13352359
                    */
                }
                catch (Exception e)
                {
                    this.Logger.LogDebug(e, "An unexpected error occurred while listening for incoming requests.");
                }
            }
        }

        protected async void RequestHandlerAsync(object state)
        {
            // 1. Create context
            var context = Options.HttpContextFactory(state, TokenSource.Token);
            this.Logger.LogTrace($"{context.Id} : Request Received {context.Request.Name}");

            // 2. Apply global response headers
            this.ApplyGlobalResponseHeaders(context.Response.Headers);

            // 3. Execute OnRequest event handlers
            try
            {
                this.Logger.LogTrace($"{context.Id} : Invoking OnRequest Handlers for {context.Request.Name}");
                var count = (this.OnRequestAsync != null) ? await this.OnRequestAsync.Invoke(context, this) : 0;
                this.Logger.LogTrace($"{context.Id} : {count} OnRequest Handlers Invoked for {context.Request.Name}");
            }
            catch (System.Net.HttpListenerException hl) when (hl.ErrorCode == 1229)
            {
                this.Logger.LogDebug($"{context.Id} : The remote connection was closed before a response could be sent for {context.Request.Name}.");
            }
            catch (Exception e)
            {
                this.Logger.LogError(e, $"{context.Id} An exception occurred while routing request {context.Request.Name}");
            }

            // 4. Optionally route request
            if (!context.WasRespondedTo)
            {
                Logger.LogTrace($"{context.Id} : Routing request {context.Request.Name}");
                ThreadPool.QueueUserWorkItem(this.Router.RouteAsync, context);
            }
        }
    }
}