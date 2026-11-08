using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Контекст хранения пользователей, ролей и связанных данных ASP.NET Core Identity.
/// </summary>
/// <param name="options">Настройки контекста базы данных.</param>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : IdentityDbContext<IdentityUserEntity, IdentityRole, string>(options)
{
    /// <summary>
    /// Настраивает модель Identity и ограничения, обеспечиваемые PostgreSQL.
    /// </summary>
    /// <param name="builder">Построитель модели Entity Framework Core.</param>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<IdentityUserEntity>()
            .HasIndex(user => user.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("EmailIndex");

        builder.Entity<IdentityRole>().HasData(
            new IdentityRole
            {
                Id = "system-role-user",
                Name = "user",
                NormalizedName = "USER",
                ConcurrencyStamp = "0b4156cb-800e-4b79-a3a6-c155fe63e8a4"
            },
            new IdentityRole
            {
                Id = "system-role-admin",
                Name = "admin",
                NormalizedName = "ADMIN",
                ConcurrencyStamp = "41541f97-6707-49d5-893e-48f82901c11c"
            });
    }
}
