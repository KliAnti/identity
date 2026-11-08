using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Identity.Infrastructure.Persistence.Models.Role;

namespace Identity.Infrastructure.Persistence.Stores;

using Identity.Infrastructure.Persistence.Models.Role;

/// <summary>
/// Адаптирует модель ролей к хранилищу ролей ASP.NET Core Identity.
/// </summary>
public sealed class IdentityRoleStore(IdentityDbContext context) : IRoleStore<RoleModel>
{
    /// <summary>
    /// Создаёт роль в базе данных.
    /// </summary>
    public async Task<IdentityResult> CreateAsync(RoleModel role, CancellationToken cancellationToken)
    {
        context.Roles.Add(role);
        await context.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>
    /// Сохраняет изменения роли.
    /// </summary>
    public async Task<IdentityResult> UpdateAsync(RoleModel role, CancellationToken cancellationToken)
    {
        context.Roles.Update(role);
        await context.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>
    /// Удаляет роль и связанные назначения.
    /// </summary>
    public async Task<IdentityResult> DeleteAsync(RoleModel role, CancellationToken cancellationToken)
    {
        context.Roles.Remove(role);
        await context.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>
    /// Возвращает строковое представление ID роли.
    /// </summary>
    public Task<string> GetRoleIdAsync(RoleModel role, CancellationToken cancellationToken) =>
        Task.FromResult(role.Id.ToString());

    /// <summary>
    /// Возвращает имя роли.
    /// </summary>
    public Task<string?> GetRoleNameAsync(RoleModel role, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(role.Name);

    /// <summary>
    /// Устанавливает имя роли.
    /// </summary>
    public Task SetRoleNameAsync(
        RoleModel role,
        string? roleName,
        CancellationToken cancellationToken)
    {
        role.Name = roleName ?? string.Empty;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Возвращает нормализованное имя роли.
    /// </summary>
    public Task<string?> GetNormalizedRoleNameAsync(RoleModel role, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(role.NormalizedName);

    /// <summary>
    /// Устанавливает нормализованное имя роли.
    /// </summary>
    public Task SetNormalizedRoleNameAsync(
        RoleModel role,
        string? normalizedName,
        CancellationToken cancellationToken)
    {
        role.NormalizedName = normalizedName ?? string.Empty;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Ищет роль по строковому идентификатору.
    /// </summary>
    public Task<RoleModel?> FindByIdAsync(string roleId, CancellationToken cancellationToken) =>
        Guid.TryParse(roleId, out var id)
            ? context.Roles.FirstOrDefaultAsync(role => role.Id == id, cancellationToken)
            : Task.FromResult<RoleModel?>(null);

    /// <summary>
    /// Ищет роль по нормализованному имени.
    /// </summary>
    public Task<RoleModel?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken) =>
        context.Roles.FirstOrDefaultAsync(
            role => role.NormalizedName == normalizedRoleName,
            cancellationToken);

    /// <summary>
    /// Освобождает ресурсы хранилища ролей.
    /// </summary>
    public void Dispose()
    {
    }
}
