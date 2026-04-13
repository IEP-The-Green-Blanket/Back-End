using Microsoft.EntityFrameworkCore;
using GB.Domain.Entities;

namespace GB.Infrastructure;

public class GreenBlanketDbContext : DbContext
{
    public GreenBlanketDbContext(DbContextOptions<GreenBlanketDbContext> options)
        : base(options) { }

    public DbSet<ForumReport> ForumReports { get; set; }
    public DbSet<UserAccount> UserAccounts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ==========================================
        // THE MASTER KEY: Set the default schema
        // ==========================================
        modelBuilder.HasDefaultSchema("hartbeespoortdam");

        // 1. ForumReport Mapping
        modelBuilder.Entity<ForumReport>().ToTable("report_forum");
        modelBuilder.Entity<ForumReport>().HasKey(f => f.ReportForumId);

        modelBuilder.Entity<ForumReport>()
            .Property(f => f.ReportForumId)
            .HasColumnName("report_forum_id");

        modelBuilder.Entity<ForumReport>()
            .Property(f => f.ReportOptionsId)
            .HasColumnName("report_options_id");

        modelBuilder.Entity<ForumReport>().Property(f => f.Name).HasColumnName("name");
        modelBuilder.Entity<ForumReport>().Property(f => f.Email).HasColumnName("email");
        modelBuilder.Entity<ForumReport>().Property(f => f.Message).HasColumnName("message");
        modelBuilder.Entity<ForumReport>().Property(f => f.Location).HasColumnName("location");
        modelBuilder.Entity<ForumReport>().Property(f => f.Date).HasColumnName("date");

        // 2. UserAccount Mapping
        modelBuilder.Entity<UserAccount>().ToTable("users");
        modelBuilder.Entity<UserAccount>().HasKey(u => u.Id);

        modelBuilder.Entity<UserAccount>()
            .Property(u => u.Id)
            .HasColumnName("user_id");

        modelBuilder.Entity<UserAccount>()
            .Property(u => u.Username)
            .HasColumnName("username");

        modelBuilder.Entity<UserAccount>()
            .Property(u => u.Email)
            .HasColumnName("user_email");

        modelBuilder.Entity<UserAccount>()
            .Property(u => u.Password)
            .HasColumnName("user_password");

        // THE FIX: Added .HasConversion<string>() to map the Enum to PostgreSQL's character varying column
        modelBuilder.Entity<UserAccount>()
            .Property(u => u.Role)
            .HasColumnName("user_role")
            .HasConversion<string>();
    }
}