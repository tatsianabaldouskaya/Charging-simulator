using ChargingSimulator.Application.Abstractions;
using ChargingSimulator.Application.Identifiers;
using ChargingSimulator.Application.Sessions;
using ChargingSimulator.Host.Configuration;
using ChargingSimulator.Host.Endpoints;
using ChargingSimulator.Host.Manual;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<SimulatorOptions>(builder.Configuration.GetSection(SimulatorOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IIdentifierGenerator, GuidIdentifierGenerator>();
builder.Services.AddSingleton<ManualOcppConnectionRegistry>();
builder.Services.AddSingleton(services =>
{
    SimulatorOptions options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<SimulatorOptions>>().Value;
    options.Validate();
    return new SessionManager(services.GetRequiredService<TimeProvider>(), services.GetRequiredService<IIdentifierGenerator>(), options.HistoryLimit, TimeSpan.FromMinutes(options.LeaseMinutes));
});
WebApplication app = builder.Build();

app.UseExceptionHandler(errors => errors.Run(async context =>
{
    Exception? error = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    SessionException? sessionError = error as SessionException;
    context.Response.StatusCode = sessionError?.Status ?? StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
    {
        Type = $"https://charging-simulator/errors/{sessionError?.Code ?? "internal-error"}",
        Title = sessionError?.Message ?? "Unexpected server error",
        Status = context.Response.StatusCode,
        Detail = sessionError?.Message
    });
}));
app.UseDefaultFiles(); app.UseStaticFiles();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapManualControl();
app.Run();
