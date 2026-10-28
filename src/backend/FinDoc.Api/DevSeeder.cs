using FinDoc.Domain.Entities;
using FinDoc.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FinDoc.Api;

// Development-only demo data so tenant isolation can be tried by hand. Requires Seed:Password (user-secrets or env var).
public static class DevSeeder
{
    public static async Task SeedAsync(IServiceProvider services, string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        await db.Database.MigrateAsync();

        await SeedUserAsync("Acme", "admin@acme.test", TenantRole.TenantAdmin);
        await SeedUserAsync("Acme", "analyst@acme.test", TenantRole.Analyst);
        await SeedUserAsync("Globex", "admin@globex.test", TenantRole.TenantAdmin);

        async Task SeedUserAsync(string tenantName, string email, TenantRole role)
        {
            var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Name == tenantName);
            if (tenant is null)
            {
                tenant = new Tenant { Name = tenantName };
                db.Tenants.Add(tenant);
                await db.SaveChangesAsync();
            }

            var user = await users.FindByEmailAsync(email);
            if (user is null)
            {
                user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
                var result = await users.CreateAsync(user, password);
                if (!result.Succeeded)
                    throw new InvalidOperationException("Seed user creation failed: " + string.Join("; ", result.Errors.Select(e => e.Code)));
            }

            if (!await db.Memberships.AnyAsync(m => m.UserId == user.Id))
            {
                db.Memberships.Add(new TenantMembership { TenantId = tenant.Id, UserId = user.Id, Role = role });
                await db.SaveChangesAsync();
            }
        }
    }
}
