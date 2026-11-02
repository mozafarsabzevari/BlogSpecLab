using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Infra.DataAccess.Identity;
using Microsoft.Data.Sqlite;

namespace BlogSpecLab.Endpoints.Api.Provisioning;

/// <summary>
/// Local command-line provisioning. Runs only in Development, never applies migrations, and prints each raw
/// token exactly once.
/// </summary>
public static class ProvisioningCommands
{
    private const string ProvisionUser = "provision-user";
    private const string IssueToken = "issue-token";

    public static bool IsCommand(string[] args) =>
        args.Length > 0 && args[0] is ProvisionUser or IssueToken;

    public static async Task<int> RunAsync(WebApplication app, string[] args)
    {
        if (!app.Environment.IsDevelopment())
        {
            Console.Error.WriteLine("Provisioning commands run only in the Development environment.");
            return 1;
        }

        if (!TryReadOptions(args.AsSpan(1), out var options, out var error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var provisioner = scope.ServiceProvider.GetRequiredService<LocalProvisioner>();

        try
        {
            return args[0] == ProvisionUser
                ? await ProvisionUserAsync(provisioner, options)
                : await IssueTokenAsync(provisioner, options);
        }
        catch (SqliteException)
        {
            Console.Error.WriteLine(
                "The database is not ready. Apply migrations first with 'dotnet ef database update'.");
            return 1;
        }
    }

    private static async Task<int> ProvisionUserAsync(LocalProvisioner provisioner, Dictionary<string, string> options)
    {
        if (!options.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
        {
            Console.Error.WriteLine("Usage: provision-user --name <name> --roles <Author,Editor> [--expires-in-days <days>]");
            return 1;
        }

        if (!options.TryGetValue("roles", out var rolesText) || !TryParseRoles(rolesText, out var roles))
        {
            Console.Error.WriteLine("--roles must be a comma-separated list of: Author, Editor.");
            return 1;
        }

        if (!TryReadLifetime(options, out var lifetime))
        {
            return 1;
        }

        var provisioned = await provisioner.ProvisionUserAsync(name, roles, lifetime);

        Console.WriteLine($"UserId: {provisioned.UserId}");
        WriteToken(provisioned.Token);
        return 0;
    }

    private static async Task<int> IssueTokenAsync(LocalProvisioner provisioner, Dictionary<string, string> options)
    {
        if (!options.TryGetValue("user-id", out var userIdText) || !Guid.TryParse(userIdText, out var userId))
        {
            Console.Error.WriteLine("Usage: issue-token --user-id <guid> [--expires-in-days <days>]");
            return 1;
        }

        if (!TryReadLifetime(options, out var lifetime))
        {
            return 1;
        }

        var rawToken = await provisioner.IssueTokenAsync(userId, lifetime);

        if (rawToken is null)
        {
            Console.Error.WriteLine("User not found.");
            return 1;
        }

        Console.WriteLine($"UserId: {userId}");
        WriteToken(rawToken);
        return 0;
    }

    private static void WriteToken(string rawToken)
    {
        Console.WriteLine($"Token: {rawToken}");
        Console.WriteLine("This token is shown only once. Store it securely and never commit it.");
    }

    private static bool TryReadOptions(
        ReadOnlySpan<string> args,
        out Dictionary<string, string> options,
        out string? error)
    {
        options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        error = null;

        for (var i = 0; i < args.Length; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
            {
                error = $"Invalid option '{args[i]}'. Options must be given as '--name value'.";
                return false;
            }

            options[args[i][2..]] = args[i + 1];
        }

        return true;
    }

    private static bool TryParseRoles(string text, out List<Role> roles)
    {
        roles = [];

        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse<Role>(part, ignoreCase: true, out var role) || !Enum.IsDefined(role))
            {
                return false;
            }

            roles.Add(role);
        }

        return roles.Count > 0;
    }

    private static bool TryReadLifetime(Dictionary<string, string> options, out TimeSpan? lifetime)
    {
        lifetime = null;

        if (!options.TryGetValue("expires-in-days", out var daysText))
        {
            return true;
        }

        if (!int.TryParse(daysText, out var days) || days <= 0)
        {
            Console.Error.WriteLine("--expires-in-days must be a positive whole number.");
            return false;
        }

        lifetime = TimeSpan.FromDays(days);
        return true;
    }
}
