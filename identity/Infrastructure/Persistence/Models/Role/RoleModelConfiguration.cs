using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Models.Role;

/// <summary>
/// Настраивает таблицу глобальных ролей.
/// </summary>
public sealed class RoleModelConfiguration : IEntityTypeConfiguration<RoleModel>
{
    /// <summary>
    /// Задаёт таблицу, ключ и уникальность нормализованного имени роли.
    /// </summary>
    /// <param name="builder">Построитель конфигурации модели роли.</param>
    public void Configure(EntityTypeBuilder<RoleModel> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Name).HasMaxLength(256).IsRequired();
        builder.Property(role => role.NormalizedName).HasMaxLength(256).IsRequired();
        builder.HasIndex(role => role.NormalizedName).IsUnique();
    }
}
