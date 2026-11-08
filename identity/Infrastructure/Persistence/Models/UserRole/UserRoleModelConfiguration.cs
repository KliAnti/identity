using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Models.UserRole;

using Identity.Infrastructure.Persistence.Models.Role;
using Identity.Infrastructure.Persistence.Models.User;

/// <summary>
/// Настраивает таблицу связей пользователей и ролей.
/// </summary>
public sealed class UserRoleModelConfiguration : IEntityTypeConfiguration<UserRoleModel>
{
    /// <summary>
    /// Задаёт составной ключ, индексы и внешние ключи связи.
    /// </summary>
    /// <param name="builder">Построитель конфигурации назначения роли.</param>
    public void Configure(EntityTypeBuilder<UserRoleModel> builder)
    {
        builder.ToTable("UserRoles");
        builder.HasKey(userRole => new { userRole.UserId, userRole.RoleId });
        builder.HasIndex(userRole => userRole.RoleId);
        builder.HasOne<UserModel>()
            .WithMany()
            .HasForeignKey(userRole => userRole.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<RoleModel>()
            .WithMany()
            .HasForeignKey(userRole => userRole.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
