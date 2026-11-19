using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Identity.Infrastructure.Persistence.Models.Role;
using Identity.Infrastructure.Persistence.Models.User;
using Identity.Infrastructure.Persistence.Models.UserRole;

namespace Identity.Infrastructure.Persistence.Stores;

using Identity.Infrastructure.Persistence.Models.Role;
using Identity.Infrastructure.Persistence.Models.User;
using Identity.Infrastructure.Persistence.Models.UserRole;

/// <summary>
/// Адаптирует пользовательскую модель к хранилищам ASP.NET Core Identity.
/// </summary>
public sealed class IdentityUserStore(IdentityDbContext context) :
    IUserStore<UserModel>,
    IUserEmailStore<UserModel>,
    IUserPasswordStore<UserModel>,
    IUserRoleStore<UserModel>
{
    /// <summary>
    /// Создаёт пользователя в базе данных.
    /// </summary>
    public async Task<IdentityResult> CreateAsync(UserModel user, CancellationToken cancellationToken)
    {
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>
    /// Сохраняет изменения пользователя.
    /// </summary>
    public async Task<IdentityResult> UpdateAsync(UserModel user, CancellationToken cancellationToken)
    {
        context.Users.Update(user);
        await context.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>
    /// Удаляет пользователя и связанные с ним данные.
    /// </summary>
    public async Task<IdentityResult> DeleteAsync(UserModel user, CancellationToken cancellationToken)
    {
        context.Users.Remove(user);
        await context.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>
    /// Возвращает строковое представление ID пользователя.
    /// </summary>
    public Task<string> GetUserIdAsync(UserModel user, CancellationToken cancellationToken) =>
        Task.FromResult(user.Id.ToString());

    /// <summary>
    /// Возвращает email пользователя как имя пользователя.
    /// </summary>
    public Task<string?> GetUserNameAsync(UserModel user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.Email);

    /// <summary>
    /// Устанавливает email пользователя как имя пользователя.
    /// </summary>
    public Task SetUserNameAsync(UserModel user, string? userName, CancellationToken cancellationToken)
    {
        user.Email = userName ?? string.Empty;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Возвращает нормализованный email пользователя.
    /// </summary>
    public Task<string?> GetNormalizedUserNameAsync(UserModel user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.NormalizedEmail);

    /// <summary>
    /// Устанавливает нормализованный email пользователя.
    /// </summary>
    public Task SetNormalizedUserNameAsync(
        UserModel user,
        string? normalizedName,
        CancellationToken cancellationToken)
    {
        user.NormalizedEmail = normalizedName ?? string.Empty;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Ищет пользователя по строковому идентификатору.
    /// </summary>
    public Task<UserModel?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
        Guid.TryParse(userId, out var id)
            ? context.Users.FirstOrDefaultAsync(user => user.Id == id, cancellationToken)
            : Task.FromResult<UserModel?>(null);

    /// <summary>
    /// Ищет пользователя по нормализованному имени пользователя.
    /// </summary>
    public Task<UserModel?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
        context.Users.FirstOrDefaultAsync(
            user => user.NormalizedEmail == normalizedUserName,
            cancellationToken);

    /// <summary>
    /// Устанавливает адрес электронной почты пользователя.
    /// </summary>
    public Task SetEmailAsync(UserModel user, string? email, CancellationToken cancellationToken)
    {
        user.Email = email ?? string.Empty;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Возвращает адрес электронной почты пользователя.
    /// </summary>
    public Task<string?> GetEmailAsync(UserModel user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.Email);

    /// <summary>
    /// Возвращает признак подтверждения email.
    /// </summary>
    public Task<bool> GetEmailConfirmedAsync(UserModel user, CancellationToken cancellationToken) =>
        Task.FromResult(user.EmailConfirmed);

    /// <summary>
    /// Устанавливает признак подтверждения email.
    /// </summary>
    public Task SetEmailConfirmedAsync(
        UserModel user,
        bool confirmed,
        CancellationToken cancellationToken)
    {
        user.EmailConfirmed = confirmed;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Ищет пользователя по нормализованному email.
    /// </summary>
    public Task<UserModel?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        context.Users.FirstOrDefaultAsync(
            user => user.NormalizedEmail == normalizedEmail,
            cancellationToken);

    /// <summary>
    /// Возвращает нормализованный email пользователя.
    /// </summary>
    public Task<string?> GetNormalizedEmailAsync(UserModel user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.NormalizedEmail);

    /// <summary>
    /// Устанавливает нормализованный email пользователя.
    /// </summary>
    public Task SetNormalizedEmailAsync(
        UserModel user,
        string? normalizedEmail,
        CancellationToken cancellationToken)
    {
        user.NormalizedEmail = normalizedEmail ?? string.Empty;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Устанавливает хеш пароля пользователя.
    /// </summary>
    public Task SetPasswordHashAsync(
        UserModel user,
        string? passwordHash,
        CancellationToken cancellationToken)
    {
        user.PasswordHash = passwordHash;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Возвращает хеш пароля пользователя.
    /// </summary>
    public Task<string?> GetPasswordHashAsync(UserModel user, CancellationToken cancellationToken) =>
        Task.FromResult(user.PasswordHash);

    /// <summary>
    /// Проверяет, сохранены ли у пользователя хеш и соль пароля.
    /// </summary>
    public Task<bool> HasPasswordAsync(UserModel user, CancellationToken cancellationToken) =>
        Task.FromResult(user.PasswordHash is not null && user.PasswordSalt is not null);

    /// <summary>
    /// Назначает пользователю роль по её нормализованному имени.
    /// </summary>
    public async Task AddToRoleAsync(
        UserModel user,
        string normalizedRoleName,
        CancellationToken cancellationToken)
    {
        var role = await context.Roles.FirstOrDefaultAsync(
            candidate => candidate.NormalizedName == normalizedRoleName,
            cancellationToken) ?? throw new InvalidOperationException(
                $"Role '{normalizedRoleName}' does not exist.");

        var exists = await context.UserRoles.AnyAsync(
            userRole => userRole.UserId == user.Id && userRole.RoleId == role.Id,
            cancellationToken);
        if (!exists)
        {
            context.UserRoles.Add(new UserRoleModel { UserId = user.Id, RoleId = role.Id });
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Удаляет у пользователя роль по её нормализованному имени.
    /// </summary>
    public async Task RemoveFromRoleAsync(
        UserModel user,
        string normalizedRoleName,
        CancellationToken cancellationToken)
    {
        var role = await context.Roles.FirstOrDefaultAsync(
            candidate => candidate.NormalizedName == normalizedRoleName,
            cancellationToken);
        if (role is null)
        {
            return;
        }

        var userRole = await context.UserRoles.FirstOrDefaultAsync(
            candidate => candidate.UserId == user.Id && candidate.RoleId == role.Id,
            cancellationToken);
        if (userRole is not null)
        {
            context.UserRoles.Remove(userRole);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Возвращает имена ролей пользователя.
    /// </summary>
    public async Task<IList<string>> GetRolesAsync(UserModel user, CancellationToken cancellationToken) =>
        await (
            from userRole in context.UserRoles
            join role in context.Roles on userRole.RoleId equals role.Id
            where userRole.UserId == user.Id
            select role.Name).ToListAsync(cancellationToken);

    /// <summary>
    /// Проверяет, назначена ли пользователю указанная роль.
    /// </summary>
    public async Task<bool> IsInRoleAsync(
        UserModel user,
        string normalizedRoleName,
        CancellationToken cancellationToken) =>
        await (
            from userRole in context.UserRoles
            join role in context.Roles on userRole.RoleId equals role.Id
            where userRole.UserId == user.Id && role.NormalizedName == normalizedRoleName
            select userRole).AnyAsync(cancellationToken);

    /// <summary>
    /// Возвращает пользователей, которым назначена указанная роль.
    /// </summary>
    public async Task<IList<UserModel>> GetUsersInRoleAsync(
        string normalizedRoleName,
        CancellationToken cancellationToken) =>
        await (
            from userRole in context.UserRoles
            join user in context.Users on userRole.UserId equals user.Id
            join role in context.Roles on userRole.RoleId equals role.Id
            where role.NormalizedName == normalizedRoleName
            select user).ToListAsync(cancellationToken);

    /// <summary>
    /// Освобождает ресурсы хранилища.
    /// </summary>
    public void Dispose()
    {
    }
}
