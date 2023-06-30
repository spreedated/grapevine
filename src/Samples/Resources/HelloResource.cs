using Grapevine;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Samples.Resources
{
    [RestResource(BasePath = "api")]
    public class HelloResource
    {
        private static int Count = 0;

        private readonly ILogger<HelloResource> _logger;

        private readonly IRestServer _server;

        public HelloResource(ILogger<HelloResource> logger, IRestServer server)
        {
            _logger = logger;
            _server = server;
            Count++;
        }

        [RestRoute("Get", "/hello", Name = "Hello, world!", Description = "The obligatory \"Hello, world!\" route")]
        public async Task HelloWorld(IHttpContext context)
        {
            _logger.LogTrace($"{context.Request.Name} : Hello, world!");
            await context.Response.SendResponseAsync($"Hello, world! ({Count})");
        }

        [RestRoute("Get", "/stop", Name = "Server Stop")]
        public async Task StopServer(IHttpContext context)
        {
            _logger.LogTrace($"{context.Request.Name} : Stopping Server");
            await context.Response.SendResponseAsync("Stopping Server");
            _server.Stop();
        }

        [RestRoute(Name = "Default Route", Description = "The default route is diabled by default", Enabled = false)]
        public async Task DefaultRoute(IHttpContext context)
        {
            _logger.LogTrace($"{context.Request.Name} : Catch All Method");
            await context.Response.SendResponseAsync(HttpStatusCode.Ok);
        }

        [RestRoute("Get", "/static")]
        [Header("Host", "localhost:1234")]
        public static async Task StaticRoute(IHttpContext context)
        {
            await context.Response.SendResponseAsync("Successfully executed a static route on a non-static class");
        }
    }
}