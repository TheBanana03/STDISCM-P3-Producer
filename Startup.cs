using VideoProto;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Add gRPC service to the dependency injection container
        services.AddGrpc();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {

            // Optional: A fallback for non-gRPC requests
            endpoints.MapGet("/", async context =>
            {
                await context.Response.WriteAsync("This server hosts gRPC services.");
            });
        });
    }
}