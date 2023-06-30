using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;

namespace Grapevine
{
    public class HttpRequest : IHttpRequest
    {
        public HttpListenerRequest Advanced { get; }

        public string[] AcceptTypes => this.Advanced.AcceptTypes;

        public Encoding ContentEncoding => this.Advanced.ContentEncoding;

        public long ContentLength64 => this.Advanced.ContentLength64;

        public string ContentType => this.Advanced.ContentType;

        public CookieCollection Cookies => this.Advanced.Cookies;

        public bool HasEntityBody => this.Advanced.HasEntityBody;

        public NameValueCollection Headers => this.Advanced.Headers;

        public string HostPrefix { get; }

        public HttpMethod HttpMethod => this.Advanced.HttpMethod;

        public Stream InputStream => this.Advanced.InputStream;

        public string MultipartBoundary { get; }

        public string Name => $"{this.HttpMethod} {this.Endpoint}";

        public string Endpoint { get; protected set; }

        public IDictionary<string, string> PathParameters { get; set; } = new Dictionary<string, string>();

        public NameValueCollection QueryString => this.Advanced.QueryString;

        public string RawUrl => this.Advanced.RawUrl;

        public IPEndPoint RemoteEndPoint => this.Advanced.RemoteEndPoint;

        public Uri Url => this.Advanced.Url;

        public Uri UrlReferrer => this.Advanced.UrlReferrer;

        public string UserAgent => this.Advanced.UserAgent;

        public string UserHostAddress => this.Advanced.UserHostAddress;

        public string UserHostname => this.Advanced.UserHostName;

        public string[] UserLanguages => this.Advanced.UserLanguages;

        public HttpRequest(HttpListenerRequest request)
        {
            this.Advanced = request;
            this.Endpoint = request.Url.AbsolutePath.TrimEnd('/');
            this.HostPrefix = request.Url.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);
            this.MultipartBoundary = this.GetMultipartBoundary();
        }
    }
}