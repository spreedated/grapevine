using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Grapevine
{
    public abstract class HttpResponseBase : IHttpResponse
    {
        public HttpListenerResponse Advanced { get; }

        public Encoding ContentEncoding
        {
            get { return this.Advanced.ContentEncoding; }
            set { this.Advanced.ContentEncoding = value; }
        }

        public TimeSpan ContentExpiresDuration { get; set; } = TimeSpan.FromDays(1);

        public long ContentLength64
        {
            get { return this.Advanced.ContentLength64; }
            set { this.Advanced.ContentLength64 = value; }
        }

        public string ContentType
        {
            get { return this.Advanced.ContentType; }
            set { this.Advanced.ContentType = value; }
        }

        public CookieCollection Cookies
        {
            get { return this.Advanced.Cookies; }
            set { this.Advanced.Cookies = value; }
        }

        public WebHeaderCollection Headers
        {
            get { return this.Advanced.Headers; }
            set { this.Advanced.Headers = value; }
        }

        public string RedirectLocation
        {
            get { return this.Advanced.RedirectLocation; }
            set { this.Advanced.RedirectLocation = value; }
        }

        public bool ResponseSent { get; protected internal set; }

        public int StatusCode
        {
            get { return this.Advanced.StatusCode; }
            set
            {
                this.Advanced.StatusDescription = (HttpStatusCode)value;
                this.Advanced.StatusCode = value;
            }
        }

        public string StatusDescription
        {
            get { return this.Advanced.StatusDescription; }
            set { this.Advanced.StatusDescription = value; }
        }

        public bool SendChunked
        {
            get { return this.Advanced.SendChunked; }
            set { this.Advanced.SendChunked = value; }
        }

        public void Abort()
        {
            this.ResponseSent = true;
            this.Advanced.Abort();
        }

        public void AddHeader(string name, string value) => this.Advanced.AddHeader(name, value);

        public void AppendCookie(Cookie cookie) => this.Advanced.AppendCookie(cookie);

        public void AppendHeader(string name, string value) => this.Advanced.AppendHeader(name, value);

        public void Redirect(string url)
        {
            this.ResponseSent = true;
            this.Advanced.Redirect(url);
        }

        public abstract Task SendResponseAsync(byte[] contents);

        public void SetCookie(Cookie cookie) => this.Advanced.SetCookie(cookie);

        protected HttpResponseBase(HttpListenerResponse response)
        {
            this.Advanced = response;
            response.ContentEncoding = Encoding.UTF8;
        }

        /// <summary>
        /// Use this method to manually set the ResponseSent property to true. Generally, the value of this property is set by the SendResponseAsync method. If, however, this method was bypassed and the this.Advanced property was used to directly access the output stream, then the ResponseSent property will need to be set here to avoid an exception being thrown later in the routing process.
        /// </summary>
        public virtual void MarkAsResponseSent()
        {
            this.ResponseSent = true;
        }
    }

    public class HttpResponse : HttpResponseBase, IHttpResponse
    {
        public CompressionProvider CompressionProvider { get; set; }

        public HttpResponse(HttpListenerResponse response) : base(response) { }

        public virtual async Task<byte[]> CompressContentsAsync(byte[] contents)
        {
            if (this.ContentType != null && ((ContentType)this.ContentType).IsBinary) return contents;
            if (contents.Length <= CompressionProvider.CompressIfContentLengthGreaterThan) return contents;

            this.Headers["Content-Encoding"] = this.CompressionProvider.ContentEncoding;
            return await this.CompressionProvider.CompressAsync(contents);
        }

        public async override Task SendResponseAsync(byte[] contents)
        {
            if (!this.Advanced.OutputStream.CanWrite) throw new NotSupportedException("The response output stream can not be written to. It may have previously been written to, or the connection may no longer be available.");

            try
            {
                contents = await this.CompressContentsAsync(contents);
                this.ContentLength64 = contents.Length;

                await this.Advanced.OutputStream.WriteAsync(contents, 0, (int)this.ContentLength64);
                this.Advanced.OutputStream.Close();
            }
            catch (StatusCodeException sce)
            {
                this.StatusCode = sce.StatusCode;
            }
            catch
            {
                if (this.Advanced.OutputStream.CanWrite) this.Advanced.OutputStream.Close();
                throw;
            }
            finally
            {
                this.ResponseSent = true;
                this.Advanced.Close();
            }
        }
    }
}