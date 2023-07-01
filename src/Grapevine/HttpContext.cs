using System;
using System.Net;
using System.Threading;

namespace Grapevine
{
    public class HttpContext : IHttpContext
    {
        public HttpListenerContext Advanced { get; }

        public CancellationToken CancellationToken { get; }

        public string Id { get; } = Guid.NewGuid().ToString();

        public Locals Locals { get; set; } = new Locals();

        public bool WasRespondedTo => this.Response.ResponseSent;

        public IHttpRequest Request { get; }

        public IHttpResponse Response { get; }

        public IServiceProvider Services { get; set; }

        internal HttpContext(HttpListenerContext context, CancellationToken token)
        {
            this.Advanced = context;
            this.CancellationToken = token;

            // https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Accept-Encoding
            var acceptEncoding = context.Request.Headers.GetValue<string>("Accept-Encoding", string.Empty);
            var identityForbidden = (acceptEncoding.Contains("identity;q=0") || acceptEncoding.Contains("*;q=0"));

            this.Request = new HttpRequest(context.Request);
            this.Response = new HttpResponse(context.Response)
            {
                CompressionProvider = new CompressionProvider(QualityValues.Parse(acceptEncoding), identityForbidden),
            };
        }
    }
}