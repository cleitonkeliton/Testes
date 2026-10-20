using Cstic.KnowledgeBase.Models;
using Microsoft.EntityFrameworkCore;

namespace Cstic.KnowledgeBase.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<Pop> Pops => Set<Pop>();
    public DbSet<PopEvent> PopEvents => Set<PopEvent>();
    public DbSet<PopVersion> PopVersions => Set<PopVersion>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<UserAccount>(user =>
        {
            user.HasIndex(u => u.Login).IsUnique();
            user.Property(u => u.Login).HasMaxLength(60);
            user.Property(u => u.FullName).HasMaxLength(120);
            user.Property(u => u.Function).HasMaxLength(40);
            user.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
            user.Property(u => u.Status).HasConversion<string>().HasMaxLength(20);
            user.Ignore(u => u.IsAdmin);
        });
        model.Entity<Pop>(pop =>
        {
            pop.HasIndex(p => p.Code).IsUnique();
            pop.HasIndex(p => p.Status);
            pop.Property(p => p.Code).HasMaxLength(40);
            pop.Property(p => p.Version).HasMaxLength(11);
            pop.Property(p => p.Title).HasMaxLength(160);
            pop.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            pop.HasOne(p => p.Author).WithMany().HasForeignKey(p => p.AuthorId).OnDelete(DeleteBehavior.Restrict);
            pop.HasMany(p => p.Events).WithOne().HasForeignKey(e => e.PopId);
            pop.HasMany(p => p.Versions).WithOne().HasForeignKey(v => v.PopId);
            pop.Ignore(p => p.IsEditable);
        });
        model.Entity<PopEvent>(item =>
        {
            item.Property(e => e.Type).HasConversion<string>().HasMaxLength(20);
            item.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PopVersion>(version =>
            version.HasOne(v => v.ValidatedBy).WithMany().HasForeignKey(v => v.ValidatedById).OnDelete(DeleteBehavior.Restrict));
    }
}
