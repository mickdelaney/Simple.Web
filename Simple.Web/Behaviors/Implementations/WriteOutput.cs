﻿namespace Simple.Web.Behaviors.Implementations
{
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Behaviors;
    using CodeGeneration;
    using Helpers;
    using Http;
    using MediaTypeHandling;

    /// <summary>
    /// This type supports the framework directly and should not be used from your code.
    /// </summary>
    public static class WriteOutput
    {
        /// <summary>
        /// This method supports the framework directly and should not be used from your code
        /// </summary>
        /// <param name="handler">The handler.</param>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public static void Impl<T>(IOutput<T> handler, IContext context)
        {
            if (typeof(T) == typeof(RawHtml))
            {
                WriteRawHtml((IOutput<RawHtml>)handler, context);
                return;
            }
            WriteUsingMediaTypeHandler(handler, context);
        }

        private static void WriteUsingMediaTypeHandler<T>(IOutput<T> handler, IContext context)
        {
            IMediaTypeHandler mediaTypeHandler;
            if (TryGetMediaTypeHandler(context, out mediaTypeHandler))
            {
                context.Response.SetContentType(mediaTypeHandler.GetContentType(context.Request.Headers[HeaderKeys.Accept]));
                if (context.Request.HttpMethod.Equals("HEAD")) return;

                context.Response.WriteFunction = (stream, token) =>
                    {
                        var content = new Content(handler, handler.Output);
                        return mediaTypeHandler.Write(content, stream);
                    };
            }
        }

        private static bool TryGetMediaTypeHandler(IContext context, out IMediaTypeHandler mediaTypeHandler)
        {
            try
            {
                string matchedType;
                mediaTypeHandler = new MediaTypeHandlerTable().GetMediaTypeHandler(context.Request.Headers[HeaderKeys.Accept], out matchedType);
            }
            catch (UnsupportedMediaTypeException)
            {
                context.Response.Status = "415 Unsupported media type requested.";
                mediaTypeHandler = null;
                return false;
            }
            return true;
        }

        internal static void WriteRawHtml(IOutput<RawHtml> handler, IContext context)
        {
            context.Response.SetContentType(
                context.Request.Headers[HeaderKeys.Accept].FirstOrDefault(
                    at => at == MediaType.Html || at == MediaType.XHtml) ?? "text/html");
            if (context.Request.HttpMethod.Equals("HEAD")) return;
            context.Response.WriteFunction = (stream, token) =>
                {
                    var bytes = Encoding.UTF8.GetBytes(handler.Output.ToString());
                    return stream.WriteAsync(bytes, 0, bytes.Length);
                };
        }
    }
    
    /// <summary>
    /// This type supports the framework directly and should not be used from your code.
    /// </summary>
    public static class WriteOutputAsync
    {
        /// <summary>
        /// This method supports the framework directly and should not be used from your code
        /// </summary>
        /// <param name="handler">The handler.</param>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public static Task Impl<T>(IOutputAsync<T> handler, IContext context)
        {
            if (typeof(T) == typeof(RawHtml))
            {
                return WriteRawHtml((IOutputAsync<RawHtml>)handler, context);
            }
            return WriteUsingMediaTypeHandler(handler, context);
        }

        private static Task WriteUsingMediaTypeHandler<T>(IOutputAsync<T> handler, IContext context)
        {
            IMediaTypeHandler mediaTypeHandler;
            if (TryGetMediaTypeHandler(context, out mediaTypeHandler))
            {
                context.Response.SetContentType(mediaTypeHandler.GetContentType(context.Request.Headers[HeaderKeys.Accept]));
                if (context.Request.HttpMethod.Equals("HEAD")) return TaskHelper.Completed();

                context.Response.WriteFunction = (stream, token) =>
                    {
                        var content = new Content(handler, handler.Output);
                        return mediaTypeHandler.Write(content, stream);
                    };
            }
            return TaskHelper.Completed();
        }

        private static bool TryGetMediaTypeHandler(IContext context, out IMediaTypeHandler mediaTypeHandler)
        {
            try
            {
                string matchedType;
                mediaTypeHandler = new MediaTypeHandlerTable().GetMediaTypeHandler(context.Request.Headers[HeaderKeys.Accept], out matchedType);
            }
            catch (UnsupportedMediaTypeException)
            {
                context.Response.Status = "415 Unsupported media type requested.";
                mediaTypeHandler = null;
                return false;
            }
            return true;
        }

        internal static Task WriteRawHtml(IOutputAsync<RawHtml> handler, IContext context)
        {
            context.Response.SetContentType(
                context.Request.Headers[HeaderKeys.Accept].FirstOrDefault(
                    at => at == MediaType.Html || at == MediaType.XHtml) ?? "text/html");
            if (context.Request.HttpMethod.Equals("HEAD")) return TaskHelper.Completed();

            context.Response.WriteFunction = (stream, token) =>
                {
                    var bytes = Encoding.UTF8.GetBytes(handler.Output.ToString());
                    return stream.WriteAsync(bytes, 0, bytes.Length);
                };
            return TaskHelper.Completed();
        }
    }
}
