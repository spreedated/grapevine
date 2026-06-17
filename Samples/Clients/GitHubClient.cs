using FluentHttpClient;
using Grapevine;
using System.Collections.Specialized;
using System.Net.Http;
using System.Threading.Tasks;

namespace Samples.Clients
{
    [RestResource(BasePath = "github")]
    public class GitHubClient
    {
        private readonly HttpClient _client;

        public GitHubClient(HttpClient client)
        {
            this._client = client;
        }

        [RestRoute("Get", "/issues/{owner}/{repo}", Description = "Returns the JSON for the list of open issues returned for the specified repository")]
        public async Task GetIssues(IHttpContext context)
        {
            var owner = context.Request.PathParameters["owner"];
            var repo = context.Request.PathParameters["repo"];

            context.Response.ContentType = ContentType.Json;

            await context.Response.SendResponseAsync
            (
                await this._client
                    .UsingRoute($"/repos/{owner}/{repo}/issues").GetAsync().ReadContentAsStreamAsync()
            );
        }
    }
}