namespace ReverseProxy.Middleware;

using System.Text;

using Microsoft.Extensions.Primitives;

internal class ReverseProxyMiddleware
{
    private static readonly HttpClient HttpClient = new();
    private readonly RequestDelegate nextMiddleware;

    public ReverseProxyMiddleware(RequestDelegate nextMiddleware)
    {
        this.nextMiddleware = nextMiddleware;
    }

    public async Task Invoke(HttpContext context)
    {
        Uri? targetUri = BuildTargetUri(context.Request);

        if (targetUri != null)
        {
            HttpRequestMessage targetRequestMessage = CreateTargetMessage(context, targetUri);

            using HttpResponseMessage responseMessage =
                await ReverseProxyMiddleware.HttpClient.SendAsync(targetRequestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);

            context.Response.StatusCode = (int)responseMessage.StatusCode;
            CopyFromTargetResponseHeaders(context, responseMessage);
            await responseMessage.Content.CopyToAsync(context.Response.Body);
            // await ProcessResponseContent(context, responseMessage);
            return;
        }

        await this.nextMiddleware(context);
    }

    private static Uri? BuildTargetUri(HttpRequest request)
    {
        Uri? targetUri = null;

        if (request.Path.StartsWithSegments("/googleforms", out PathString remainingPath))
        {
            targetUri = new Uri("https://docs.google.com/forms" + remainingPath);
        }

        return targetUri;
    }

    private static void CopyFromOriginalRequestContentAndHeaders(HttpContext context, HttpRequestMessage requestMessage)
    {
        string requestMethod = context.Request.Method;

        if (!HttpMethods.IsGet(requestMethod) &&
            !HttpMethods.IsHead(requestMethod) &&
            !HttpMethods.IsDelete(requestMethod) &&
            !HttpMethods.IsTrace(requestMethod))
        {
            var streamContent = new StreamContent(context.Request.Body);
            requestMessage.Content = streamContent;
        }

        foreach (KeyValuePair<string, StringValues> header in context.Request.Headers)
        {
            requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }
    }

    private static void CopyFromTargetResponseHeaders(HttpContext context, HttpResponseMessage responseMessage)
    {
        foreach (KeyValuePair<string, IEnumerable<string>> header in responseMessage.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        foreach (KeyValuePair<string, IEnumerable<string>> header in responseMessage.Content.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        context.Response.Headers.Remove("transfer-encoding");
    }

    private static HttpRequestMessage CreateTargetMessage(HttpContext context, Uri targetUri)
    {
        var requestMessage = new HttpRequestMessage();
        CopyFromOriginalRequestContentAndHeaders(context, requestMessage);

        requestMessage.RequestUri = targetUri;
        requestMessage.Headers.Host = targetUri.Host;
        requestMessage.Method = GetMethod(context.Request.Method);

        return requestMessage;
    }

    private static HttpMethod GetMethod(string method)
    {
        HttpMethod httpMethod = method switch
        {
            "CONNECT" => HttpMethod.Connect,
            "DELETE" => HttpMethod.Delete,
            "GET" => HttpMethod.Get,
            "HEAD" => HttpMethod.Head,
            "OPTIONS" => HttpMethod.Options,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "TRACE" => HttpMethod.Trace,
            _ => new HttpMethod(method),
        };

        return httpMethod;
    }

    private static bool IsContentOfType(HttpResponseMessage responseMessage, string type)
    {
        var result = false;

        if (responseMessage.Content.Headers.ContentType != null)
        {
            result = responseMessage.Content.Headers.ContentType.MediaType == type;
        }

        return result;
    }

    private static async Task ProcessResponseContent(HttpContext context, HttpResponseMessage responseMessage)
    {
        byte[] content = await responseMessage.Content.ReadAsByteArrayAsync();

        if (IsContentOfType(responseMessage, "text/html") ||
            IsContentOfType(responseMessage, "text/javascript"))
        {
            string stringContent = Encoding.UTF8.GetString(content);

            string newContent = stringContent.Replace("https://www.google.com", "/google")
                .Replace("https://www.gstatic.com", "/googlestatic")
                .Replace("https://docs.google.com/forms", "/googleforms");

            await context.Response.WriteAsync(newContent, Encoding.UTF8);
        }
        else
        {
            await context.Response.Body.WriteAsync(content);
        }
    }
}