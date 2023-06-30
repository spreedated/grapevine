using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Grapevine.Client
{
    public class RestRequestBuilder
    {
        private readonly HttpClient _client;

        public HttpContent Content
        {
            get { return this.Request.Content; }
            set { this.Request.Content = value; }
        }

        public Cookies Cookies { get; } = new Cookies();

        public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>();

        public QueryParams QueryParams { get; set; } = new QueryParams();

        public HttpRequestMessage Request { get; } = new HttpRequestMessage();

        public string Route { get; set; } = "";

        public TimeSpan Timeout
        {
            get { return this._client.Timeout; }
            set { this._client.Timeout = value; }
        }

        internal RestRequestBuilder(HttpClient client)
        {
            this._client = client;
        }

        public async Task<HttpResponseMessage> SendAsync(HttpMethod method, CancellationToken? token = null)
        {
            this.Request.Method = method;

            this.Headers["Cookie"] = (this.Headers.ContainsKey("Cookie"))
                ? string.Join("; ", this.Headers["Cookies"], this.Cookies.ToString())
                : this.Cookies.ToString();

            foreach (var item in this.Headers) this.Request.Headers.Add(item.Key, item.Value);

            if (this.Request.Content is MultipartContent) this.Request.Headers.ExpectContinue = false;

            this.Request.RequestUri = (!string.IsNullOrWhiteSpace(this._client.BaseAddress?.ToString()))
                ? new Uri($"{this._client.BaseAddress.ToString().TrimEnd('/')}/{this.Route.TrimStart('/')}{this.QueryParams}")
                : new Uri($"{this.Route}{this.QueryParams}");

            token ??= CancellationToken.None;
            return await this._client.SendAsync(this.Request, HttpCompletionOption.ResponseContentRead, token.Value).ConfigureAwait(false);
        }
    }
}