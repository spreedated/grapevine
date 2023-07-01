using Grapevine;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using HttpStatusCode = Grapevine.HttpStatusCode;

namespace Samples.Resources
{
    [RestResource(BasePath = "/cookie")]
    public class CookieResource
    {
        [RestRoute("Get", "/set/{name}/{value}")]
        public async Task SetCookie(IHttpContext context)
        {
            var name = context.Request.PathParameters["name"];
            var value = context.Request.PathParameters["value"];

            context.Response.Cookies.Add(new Cookie(name, value, "/"));
            await context.Response.SendResponseAsync(HttpStatusCode.Ok);
        }

        [RestRoute("Get", "/get/{name}")]
        public async Task GetCookie(IHttpContext context)
        {
            var name = context.Request.PathParameters["name"];
            var cookie = context.Request.Cookies.FirstOrDefault(c => c.Name == name);
            await context.Response.SendResponseAsync($"Cookie Value: {cookie?.Value}");
        }
    }
}