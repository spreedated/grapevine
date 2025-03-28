using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace Grapevine
{
    public abstract class RestServerBase : IRestServer
    {
        public IList<IContentFolder> ContentFolders { get; } = [];

        public IList<GlobalResponseHeaders> GlobalResponseHeaders { get; set; } = [];

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
        public virtual RequestReceivedEvent OnRequestAsync { get; set; } = [];

        public abstract void Dispose();

        public abstract void Start();

        public abstract void Stop();
    }
}
