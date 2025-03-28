#pragma warning disable CA1822 // Mark members as static

using Grapevine;
using System.Threading.Tasks;

namespace SampleServer
{
    [RestResource]
    internal class Routes
    {
        [RestRoute("GET", "/testresponse")]
        public async Task SampleGet(IHttpContext context)
        {
            await context.Response.SendResponseAsync("Successfully hit the test route!");
        }
    }
}
