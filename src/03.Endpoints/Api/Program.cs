using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Endpoints.Api.Authentication;
using BlogSpecLab.Endpoints.Api.Provisioning;
using BlogSpecLab.Infra.DataAccess;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("BlogSpecLab")
    ?? throw new InvalidOperationException("Connection string 'BlogSpecLab' is not configured.");

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDataAccess(connectionString);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services
    .AddAuthentication(OpaqueTokenAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, OpaqueTokenAuthenticationHandler>(
        OpaqueTokenAuthenticationHandler.SchemeName, configureOptions: null);
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (ProvisioningCommands.IsCommand(args))
{
    return await ProvisioningCommands.RunAsync(app, args);
}

app.UseExceptionHandler();
app.UseAuthentication();

// A supplied but invalid token is rejected with 401 even on endpoints that allow anonymous access.
app.Use(async (context, next) =>
{
    var result = await context.AuthenticateAsync();

    if (result.Failure is not null)
    {
        await context.ChallengeAsync();
        return;
    }

    await next(context);
});

app.UseAuthorization();

app.Run();
return 0;

public partial class Program;
