using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Infra.DataAccess.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BlogSpecLab.Infra.DataAccess;

public static class DependencyInjection
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<BlogSpecLabDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IUserAccessReader, UserAccessReader>();
        services.AddScoped<IAccessTokenReader, AccessTokenReader>();
        services.AddScoped<LocalProvisioner>();

        return services;
    }
}
