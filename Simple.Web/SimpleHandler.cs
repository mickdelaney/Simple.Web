namespace Simple.Web
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Web;

    internal class SimpleHandler<TEndpointType> : IHttpHandler
    {
        private readonly IAuthenticationProvider _authenticationProvider;
        private readonly IContext _context;
        private readonly EndpointInfo _endpointInfo;
        private readonly ContentTypeHandlerTable _contentTypeHandlerTable = new ContentTypeHandlerTable();
// ReSharper disable StaticFieldInGenericType
        private static readonly Lazy<RoutingTable> RoutingTable = new Lazy<RoutingTable>(() => new RoutingTableBuilder(typeof(TEndpointType)).BuildRoutingTable());
// ReSharper restore StaticFieldInGenericType

        public static IHttpHandler TryCreate(IContext context)
        {
            IDictionary<string, string> variables;
            var endpointType = RoutingTable.Value.Get(context.Request.Url.AbsolutePath, context.Request.AcceptTypes, out variables);
            if (endpointType == null) return null;
            var endpointInfo = new EndpointInfo(endpointType, variables, context.Request.HttpMethod);

            foreach (var key in context.Request.QueryString.AllKeys)
            {
                endpointInfo.Variables.Add(key, context.Request.QueryString[key]);
            }

            if (endpointInfo.RequiresAuthentication)
            {
                var authenticationProvider = SimpleWeb.Configuration.Container.Get<IAuthenticationProvider>() ??
                                             new AuthenticationProvider();
                return new SimpleHandler<TEndpointType>(context, endpointInfo, authenticationProvider);
            }
            else
            {
                return new SimpleHandler<TEndpointType>(context, endpointInfo);
            }
        }

        private SimpleHandler(IContext context, EndpointInfo endpointInfo) : this(context, endpointInfo, null)
        {
        }

        private SimpleHandler(IContext context, EndpointInfo endpointInfo, IAuthenticationProvider authenticationProvider)
        {
            _context = context;
            _endpointInfo = endpointInfo;
            _authenticationProvider = authenticationProvider;
        }

        public void ProcessRequest(HttpContext context)
        {
            try
            {
                Run();
            }
            catch (HttpException httpException)
            {
                context.Response.StatusCode = httpException.ErrorCode;
                context.Response.StatusDescription = httpException.Message;
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.Message);
                context.Response.StatusCode = 500;
                context.Response.StatusDescription = "Internal server error.";
            }
        }

        private void Run()
        {
            var endpoint = EndpointFactory.Instance.GetEndpoint(_endpointInfo);

            if (endpoint != null)
            {
                if (!CheckAuthentication(endpoint)) return;

                SetContext(endpoint);
                var runner = EndpointRunner.Create<TEndpointType>(endpoint);
                runner.BeforeRun(_context, this._contentTypeHandlerTable);
                RunEndpoint(runner);
            }
        }

        private bool CheckAuthentication(object endpoint)
        {
            var requireAuthentication = endpoint as IRequireAuthentication;
            if (requireAuthentication == null) return true;

            var user = _authenticationProvider.GetLoggedInUser(_context);
            if (user == null || !user.IsAuthenticated)
            {
                _context.Response.StatusCode = 401;
                _context.Response.StatusDescription = "Unauthorized";
                return false;
            }

            requireAuthentication.CurrentUser = user;
            return true;
        }

        private void SetContext(object endpoint)
        {
            var needContext = endpoint as INeedContext;
            if (needContext != null) needContext.Context = _context;
        }

        private void RunEndpoint(EndpointRunner endpoint)
        {
            var status = endpoint.Run();

            WriteStatusCode(status);

            if ((status.Code >= 301 && status.Code <= 303) || status.Code == 307)
            {
                var redirect = endpoint.Endpoint as IMayRedirect;
                if (redirect != null && !string.IsNullOrWhiteSpace(redirect.Location))
                {
                    _context.Response.Headers.Set("Location", redirect.Location);
                }
            }
            if (status.Code != 200)
            {
                return;
            }

            ResponseWriter.Write(endpoint, _context);
        }

        private void WriteStatusCode(Status status)
        {
            _context.Response.StatusCode = status.Code;
            _context.Response.StatusDescription = status.Description;
        }

        public bool IsReusable
        {
            get { return false; }
        }
    }
}