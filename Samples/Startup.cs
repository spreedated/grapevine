using Grapevine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Samples.Clients;
using System;
using System.IO;

namespace Samples
{
    public class Startup
    {
        public IConfiguration Configuration { get; private set; }

        private readonly string _serverPort = PortFinder.FindNextLocalOpenPort(1234);

        public Startup(IConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddNLog(new NLogLoggingConfiguration(this.Configuration.GetSection("NLog")));
            });

            services.AddHttpClient<GitHubClient>(c =>
            {
                c.BaseAddress = new Uri("https://api.github.com/");
                c.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
                c.DefaultRequestHeaders.Add("User-Agent", "Grapevine-Client");
            });
        }

        public void ConfigureServer(IRestServer server)
        {
            // The path to your static content
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "website");

            // The following line is shorthand for:
            //     server.ContentFolders.Add(new ContentFolder(folderPath));
            server.ContentFolders.Add(folderPath);
            server.UseContentFolders();

            server.Prefixes.Add($"http://localhost:{this._serverPort}/");

            /* Configure server to auto parse application/x-www-for-urlencoded data*/
            server.AutoParseFormUrlEncodedData();

            /* Configure Router Options (if supported by your router implementation) */
            server.Router.Options.SendExceptionMessages = true;
        }
    }
}