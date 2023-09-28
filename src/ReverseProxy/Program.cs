using ReverseProxy.Middleware;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();

app.UseMiddleware<ReverseProxyMiddleware>();

app.MapGet("/",
    async context =>
    {
        await context.Response.WriteAsync(
            "<a href='/googleforms/d/e/1FAIpQLSdJwmxHIl_OCh-CI1J68G1EVSr9hKaYFLh3dHh8TLnxjxCJWw/viewform?hl=en'>Register to receive a T-shirt</a>");
    });

await app.RunAsync();