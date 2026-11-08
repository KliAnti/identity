using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Models.Device;

using Identity.Infrastructure.Persistence.Models.User;

/// <summary>
/// Настраивает таблицу устройств и связь с владельцем.
/// </summary>
public sealed class DeviceModelConfiguration : IEntityTypeConfiguration<DeviceModel>
{
    /// <summary>
    /// Задаёт таблицу, индекс владельца и внешний ключ пользователя.
    /// </summary>
    /// <param name="builder">Построитель конфигурации модели устройства.</param>
    public void Configure(EntityTypeBuilder<DeviceModel> builder)
    {
        builder.ToTable("Devices");
        builder.HasKey(device => device.Id);
        builder.Property(device => device.Name).HasMaxLength(200);
        builder.Property(device => device.Platform).HasMaxLength(100);
        builder.HasIndex(device => device.UserId);
        builder.HasOne<UserModel>()
            .WithMany()
            .HasForeignKey(device => device.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
