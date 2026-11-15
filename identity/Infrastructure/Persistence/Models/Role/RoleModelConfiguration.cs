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
        builder.HasData(
            new RoleModel
            {
                Id = Guid.Parse("d1813e6f-c38a-43df-8728-bc345ab88f01"),
                Name = "user",
                NormalizedName = "USER"
            },
            new RoleModel
            {
                Id = Guid.Parse("72c97b31-a2c5-4486-aee7-4115d650e102"),
                Name = "admin",
                NormalizedName = "ADMIN"
            });
    }
}
