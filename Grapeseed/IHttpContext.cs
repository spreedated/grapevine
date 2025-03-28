using System;
using System.Threading;

namespace Grapevine
{
    public interface IHttpContext
    {
        CancellationToken CancellationToken { get; }

        string Id { get; }

        /// <summary>
        /// Gets or sets a key/value collection that can be used to share data within the scope of this request
        /// </summary>
        /// <value></value>
        Locals Locals { get; set; }

        /// <summary>
        /// Returns a value that indicate whether or not the client request has been responded to
        /// </summary>
        bool WasRespondedTo { get; }

        /// <summary>
        /// Gets the IHttpRequest that represents a client's request for a resource
        /// </summary>
        IHttpRequest Request { get; }

        /// <summary>
        /// Gets the IHttpResponse object that will be sent to the client in response to the client's request
        /// </summary>
        IHttpResponse Response { get; }

        /// <summary>
        /// Gets or sets the IServiceProvider object for dependency injection
        /// </summary>
        IServiceProvider Services { get; set; }
    }
}