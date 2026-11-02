using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Infra.DataAccess.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace BlogSpecLab.Tests.Api.Infrastructure;

public sealed record TestUser(Guid Id, string DisplayName, string Token);

/// <summary>
/// Sample users from quickstart.md, created directly in the test database with fresh random tokens.
/// </summary>
public sealed class TestUsers
{
    private TestUsers(TestUser authorA, TestUser authorB, TestUser editor1, TestUser editor2)
    {
        AuthorA = authorA;
        AuthorB = authorB;
        Editor1 = editor1;
        Editor2 = editor2;
    }

    public TestUser AuthorA { get; }

    public TestUser AuthorB { get; }

    /// <summary>Has both the Author and Editor roles.</summary>
    public TestUser Editor1 { get; }

    public TestUser Editor2 { get; }

    public static async Task<TestUsers> CreateAsync(BlogSpecLabApiFactory factory) =>
        new(
            await CreateUserAsync(factory, "Author A", [Role.Author]),
            await CreateUserAsync(factory, "Author B", [Role.Author]),
            await CreateUserAsync(factory, "Editor 1", [Role.Author, Role.Editor]),
            await CreateUserAsync(factory, "Editor 2", [Role.Editor]));

    public static async Task<TestUser> CreateUserAsync(
        BlogSpecLabApiFactory factory,
        string displayName,
        IReadOnlyCollection<Role> roles,
        TimeSpan? tokenLifetime = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var provisioner = scope.ServiceProvider.GetRequiredService<LocalProvisioner>();
        var provisioned = await provisioner.ProvisionUserAsync(displayName, roles, tokenLifetime);

        return new TestUser(provisioned.UserId, displayName, provisioned.Token);
    }
}
