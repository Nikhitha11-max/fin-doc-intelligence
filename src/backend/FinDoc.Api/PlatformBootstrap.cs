using FinDoc.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FinDoc.Api;

// Creates the first Platform Administrator from configuration (email in settings, password from secrets). Idempotent.
public static class PlatformBootstrap
{
    public static async Task RunAsync(IServiceProvider services, string? email, string? password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync(PlatformRoles.PlatformAdmin))
            await roles.CreateAsync(new IdentityRole(PlatformRoles.PlatformAdmin));

        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var created = await users.CreateAsync(user, password);
            if (!created.Succeeded)
                throw new InvalidOperationException("Platform administrator creation failed: " + string.Join("; ", created.Errors.Select(e => e.Code)));
        }

        if (!await users.IsInRoleAsync(user, PlatformRoles.PlatformAdmin))
            await users.AddToRoleAsync(user, PlatformRoles.PlatformAdmin);
    }
}
