using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Grapevine
{
    public abstract class RouteBase : IRoute
    {
        public string Description { get; set; }

        public bool Enabled { get; set; }

        public Dictionary<string, Regex> HeaderConditions { get; } = new();

        public HttpMethod HttpMethod { get; set; }

        public string Name { get; set; }

        public IRouteTemplate RouteTemplate { get; set; }

        protected RouteBase(HttpMethod httpMethod, IRouteTemplate routeTemplate, bool enabled, string name, string description)
        {
            this.HttpMethod = httpMethod;
            this.RouteTemplate = routeTemplate;
            this.Name = name;
            this.Description = description;
            this.Enabled = enabled;
        }

        public abstract Task InvokeAsync(IHttpContext context);

        public virtual bool IsMatch(IHttpContext context)
        {
            if (!this.Enabled || !context.Request.HttpMethod.Equivalent(this.HttpMethod) || !this.RouteTemplate.Matches(context.Request.Endpoint)) return false;

            foreach (var condition in this.HeaderConditions)
            {
                var value = context.Request.Headers.Get(condition.Key) ?? string.Empty;
                if (condition.Value.IsMatch(value)) continue;
                return false;
            }

            return true;
        }

        public virtual IRoute WithHeader(string header, Regex pattern)
        {
            this.HeaderConditions[header] = pattern;
            return this;
        }
    }

    public class Route : RouteBase, IRoute
    {
        protected Func<IHttpContext, Task> RouteAction;

        public Route(Func<IHttpContext, Task> action, HttpMethod method, string routePattern, bool enabled = true, string name = null, string description = null)
            : this(action, method, new RouteTemplate(routePattern), enabled, name, description) { }

        public Route(Func<IHttpContext, Task> action, HttpMethod method, Regex routePattern, bool enabled = true, string name = null, string description = null)
            : this(action, method, new RouteTemplate(routePattern), enabled, name, description) { }

        public Route(Func<IHttpContext, Task> action, HttpMethod method, IRouteTemplate routeTemplate, bool enabled = true, string name = null, string description = null)
            : base(method, routeTemplate, enabled, name, description)
        {
            this.RouteAction = action;
            this.Name = (string.IsNullOrWhiteSpace(name))
                ? action.Method.Name
                : name;
        }

        public override async Task InvokeAsync(IHttpContext context)
        {
            if (!this.Enabled) return;
            context.Request.PathParameters = this.RouteTemplate.ParseEndpoint(context.Request.Endpoint);
            await this.RouteAction(context).ConfigureAwait(false);
        }
    }

    public class Route<T> : RouteBase, IRoute
    {
        protected Func<T, IHttpContext, Task> RouteAction;

        public Route(MethodInfo methodInfo, HttpMethod method, string routePattern, bool enabled = true, string name = null, string description = null)
            : this(methodInfo, method, new RouteTemplate(routePattern), enabled, name, description) { }

        public Route(MethodInfo methodInfo, HttpMethod method, Regex routePattern, bool enabled = true, string name = null, string description = null)
            : this(methodInfo, method, new RouteTemplate(routePattern), enabled, name, description) { }

        public Route(MethodInfo methodInfo, HttpMethod method, IRouteTemplate routeTemplate, bool enabled = true, string name = null, string description = null) : base(method, routeTemplate, enabled, name, description)
        {
            this.RouteAction = (Func<T, IHttpContext, Task>)Delegate.CreateDelegate(typeof(Func<T, IHttpContext, Task>), null, methodInfo);
            if (string.IsNullOrWhiteSpace(this.Name)) this.Name = methodInfo.Name;
        }

        public override async Task InvokeAsync(IHttpContext context)
        {
            if (!this.Enabled) return;
            context.Request.PathParameters = this.RouteTemplate.ParseEndpoint(context.Request.Endpoint);
            await this.RouteAction(context.Services.GetRequiredService<T>(), context).ConfigureAwait(false);
        }
    }
}