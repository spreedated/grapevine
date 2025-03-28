#pragma warning disable IDE0060

using System;
using System.Threading.Tasks;

namespace Grapevine.Middleware
{
    public class CorrelationId
    {
        public static string DefaultCorrelationIdFieldName { get; set; } = "X-Correlation-Id";

        public static Func<string> DefaultCorrelationIdFactory { get; set; } = () => { return Guid.NewGuid().ToString(); };

        public string CorrelationIdFieldName { get; }

        public Func<string> CorrelationIdFactory { get; }

        public CorrelationId(string fieldName, Func<string> factory)
        {
            this.CorrelationIdFieldName = fieldName ?? DefaultCorrelationIdFieldName;
            this.CorrelationIdFactory = factory ?? DefaultCorrelationIdFactory;
        }

        public async Task EnsureCorrelationIdAsync(IHttpContext context, IRestServer server)
        {
            string value = context.Request.Headers[this.CorrelationIdFieldName] ?? this.CorrelationIdFactory();
            context.Response.AddHeader(this.CorrelationIdFieldName, value);
            context.Locals.TryAdd("CorrelationIdFieldName", this.CorrelationIdFieldName);
            context.Locals.TryAdd(this.CorrelationIdFieldName, value);
            await Task.CompletedTask;
        }
    }
}