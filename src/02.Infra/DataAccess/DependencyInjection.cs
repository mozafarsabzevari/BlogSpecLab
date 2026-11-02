using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.Posts;
using BlogSpecLab.Infra.DataAccess.Identity;
using BlogSpecLab.Infra.DataAccess.Posts;
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
        services.AddScoped<IPostRepository, PostRepository>();

        return services;
    }
}
