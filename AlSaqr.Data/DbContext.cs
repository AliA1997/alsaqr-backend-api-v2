using AlSaqr.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text.Json;

namespace AlSaqr.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users => Set<User>();

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var listConverter = new ValueConverter<List<string>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrEmpty(v)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()
            );

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);

                entity.Property(u => u.FavoriteQuranReciters).HasConversion(listConverter);
                entity.Property(u => u.IslamicStudyTopics).HasConversion(listConverter);
                entity.Property(u => u.FavoriteIslamicScholars).HasConversion(listConverter);
                entity.Property(u => u.Hobbies).HasConversion(listConverter);
                entity.Property(u => u.FollowingUsers).HasConversion(listConverter);
            });
        }
    }
}
