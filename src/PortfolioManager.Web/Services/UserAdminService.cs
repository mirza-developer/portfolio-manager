using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Data;
using PortfolioManager.Data.Identity;
using PortfolioManager.Web.Security;

namespace PortfolioManager.Web.Services;

public interface IUserAdminService
{
    Task<IReadOnlyList<UserRow>> ListAsync(UserContext admin, CancellationToken ct = default);
    Task<string> CreateAsync(UserContext admin, UserEdit edit, CancellationToken ct = default);
    Task UpdateAsync(UserContext admin, UserEdit edit, CancellationToken ct = default);
    Task ResetPasswordAsync(UserContext admin, string userId, string newPassword, CancellationToken ct = default);
    Task DeleteAsync(UserContext admin, string userId, CancellationToken ct = default);
}

public sealed class UserAdminService(IDbContextFactory<AppDbContext> factory, UserManager<ApplicationUser> users) : IUserAdminService
{
    private static readonly string[] Roles = { AppRoles.Admin, AppRoles.User };

    public async Task<IReadOnlyList<UserRow>> ListAsync(UserContext admin, CancellationToken ct = default)
    {
        Guard.RequireAdmin(admin);
        await using var db = await factory.CreateDbContextAsync(ct);
        var all = await db.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync(ct);
        var roleLinks = await (from ur in db.UserRoles.AsNoTracking()
                               join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                               select new { ur.UserId, r.Name }).ToListAsync(ct);
        return all.Select(u => new UserRow(
            u.Id, u.Email ?? u.UserName ?? string.Empty, u.DisplayName,
            roleLinks.Where(r => r.UserId == u.Id).Select(r => r.Name).FirstOrDefault() ?? string.Empty, u.IsActive)).ToList();
    }

    public async Task<string> CreateAsync(UserContext admin, UserEdit edit, CancellationToken ct = default)
    {
        Guard.RequireAdmin(admin);
        Guard.Check(!string.IsNullOrWhiteSpace(edit.Email) && edit.Email.Contains('@'), "A valid email address is required.");
        Guard.Check(Roles.Contains(edit.Role), "The role must be Admin or User.");
        Guard.Check(!string.IsNullOrEmpty(edit.Password), "A password is required for a new user.");

        var user = new ApplicationUser
        {
            UserName = edit.Email.Trim(),
            Email = edit.Email.Trim(),
            EmailConfirmed = true,
            DisplayName = edit.DisplayName?.Trim() ?? string.Empty,
            IsActive = edit.IsActive
        };
        Ensure(await users.CreateAsync(user, edit.Password!));
        Ensure(await users.AddToRoleAsync(user, edit.Role));
        return user.Id;
    }

    public async Task UpdateAsync(UserContext admin, UserEdit edit, CancellationToken ct = default)
    {
        Guard.RequireAdmin(admin);
        Guard.Check(Roles.Contains(edit.Role), "The role must be Admin or User.");
        var user = await users.FindByIdAsync(edit.Id ?? string.Empty) ?? throw new KeyNotFoundException("The user does not exist.");

        if (user.Id == admin.UserId)
        {
            Guard.Check(edit.IsActive, "You cannot deactivate your own account.");
            Guard.Check(edit.Role == AppRoles.Admin, "You cannot remove your own Admin role.");
        }

        user.DisplayName = edit.DisplayName?.Trim() ?? string.Empty;
        user.IsActive = edit.IsActive;
        Ensure(await users.UpdateAsync(user));

        var current = await users.GetRolesAsync(user);
        var toRemove = current.Where(r => Roles.Contains(r) && r != edit.Role).ToList();
        if (toRemove.Count > 0) Ensure(await users.RemoveFromRolesAsync(user, toRemove));
        if (!current.Contains(edit.Role)) Ensure(await users.AddToRoleAsync(user, edit.Role));
    }

    public async Task ResetPasswordAsync(UserContext admin, string userId, string newPassword, CancellationToken ct = default)
    {
        Guard.RequireAdmin(admin);
        var user = await users.FindByIdAsync(userId) ?? throw new KeyNotFoundException("The user does not exist.");
        if (await users.HasPasswordAsync(user)) Ensure(await users.RemovePasswordAsync(user));
        Ensure(await users.AddPasswordAsync(user, newPassword));
    }

    public async Task DeleteAsync(UserContext admin, string userId, CancellationToken ct = default)
    {
        Guard.RequireAdmin(admin);
        Guard.Check(userId != admin.UserId, "You cannot delete your own account.");
        var user = await users.FindByIdAsync(userId) ?? throw new KeyNotFoundException("The user does not exist.");

        await using var db = await factory.CreateDbContextAsync(ct);
        Guard.Check(!await db.Portfolios.AnyAsync(p => p.UserId == userId, ct), "This user owns portfolios. Deactivate the user instead.");
        Ensure(await users.DeleteAsync(user));
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }
}
