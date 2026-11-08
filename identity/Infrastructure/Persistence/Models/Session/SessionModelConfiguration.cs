using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Models.Session;

using Identity.Infrastructure.Persistence.Models.Device;

/// <summary>
/// Настраивает таблицу пользовательских сессий и связь с устройством.
/// </summary>
public sealed class SessionModelConfiguration : IEntityTypeConfiguration<SessionModel>
{
    /// <summary>
    /// Задаёт таблицу, индексы для выборки сессий и внешний ключ устройства.
    /// </summary>
    /// <param name="builder">Построитель конфигурации модели сессии.</param>
    public void Configure(EntityTypeBuilder<SessionModel> builder)
    {
        builder.ToTable("Sessions");
        builder.HasKey(session => session.Id);
        builder.HasIndex(session => new { session.DeviceId, session.ExpiresAt });
        builder.HasIndex(session => session.ExpiresAt);
        builder.HasOne<DeviceModel>()
            .WithMany()
            .HasForeignKey(session => session.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
