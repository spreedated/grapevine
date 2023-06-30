#pragma warning disable IDE0060

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Grapevine.Middleware
{
    public interface ICorsPolicy
    {
        Task ApplyAsync(IHttpContext context, IRestServer server);
    }

    public class CorsPolicy : ICorsPolicy
    {
        public static async Task CorsWildcardAsync(IHttpContext context, IRestServer server)
        {
            context.Response.AddHeader("Access-Control-Allow-Origin", "*");
            context.Response.AddHeader("Access-Control-Allow-Headers", "X-Requested-With");
            await Task.CompletedTask;
        }

        private readonly IEnumerable<string> _allowedOrigins;

        public CorsPolicy(Uri allowOrigin)
        {
            this._allowedOrigins = new List<string>() { allowOrigin.ToString() };
        }

        public CorsPolicy(IEnumerable<Uri> allowOrigins)
        {
            this._allowedOrigins = allowOrigins.Cast<string>();
        }

        public async Task ApplyAsync(IHttpContext context, IRestServer server)
        {
            if (this._allowedOrigins.Count<string>() == 1)
            {
                context.Response.AddHeader("Access-Control-Allow-Origin", this._allowedOrigins.First<string>());
                context.Response.AddHeader("Vary", "Origin");
            }
            else
            {
                var domain = context.Request.UrlReferrer?.ToString();

                if (!string.IsNullOrWhiteSpace(domain) && this._allowedOrigins.Contains(domain))
                {
                    context.Response.AddHeader("Access-Control-Allow-Origin", domain);
                    context.Response.AddHeader("Vary", "Origin");
                }
            }

            await Task.CompletedTask;
        }
    }
}