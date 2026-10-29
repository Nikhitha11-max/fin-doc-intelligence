using FinDoc.Application.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace FinDoc.Infrastructure.Persistence;

public class UserDirectory(UserManager<IdentityUser> users) : IUserDirectory
{
    public async Task<(string? UserId, string? Error)> CreateUserAsync(string email, string password, CancellationToken ct)
    {
        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await users.CreateAsync(user, password);
        if (result.Succeeded)
            return (user.Id, null);

        // Duplicate errors are hidden so the response never reveals whether an address is registered elsewhere.
        var errors = result.Errors.Where(e => !e.Code.StartsWith("Duplicate", StringComparison.Ordinal)).ToList();
        return (null, errors.Count == 0 ? null : string.Join(" ", errors.Select(e => e.Description)));
    }

    public async Task DeleteUserAsync(string userId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is not null)
            await users.DeleteAsync(user);
    }
}
