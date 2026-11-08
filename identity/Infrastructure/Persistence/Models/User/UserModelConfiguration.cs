using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Models.User;

/// <summary>
/// Настраивает таблицу пользовательских учётных записей.
/// </summary>
public sealed class UserModelConfiguration : IEntityTypeConfiguration<UserModel>
{
    /// <summary>
    /// Задаёт таблицу, ключ и уникальность нормализованного email.
    /// </summary>
    /// <param name="builder">Построитель конфигурации модели пользователя.</param>
    public void Configure(EntityTypeBuilder<UserModel> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Email).HasMaxLength(256).IsRequired();
        builder.Property(user => user.NormalizedEmail).HasMaxLength(256).IsRequired();
        builder.Property(user => user.EmailConfirmed).IsRequired();
        builder.Property(user => user.PasswordHash).IsRequired(false);
        builder.Property(user => user.PasswordSalt).HasColumnType("bytea").IsRequired(false);
        builder.HasIndex(user => user.NormalizedEmail).IsUnique();
    }
}
