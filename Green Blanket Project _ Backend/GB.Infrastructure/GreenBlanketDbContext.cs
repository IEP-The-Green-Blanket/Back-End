using Microsoft.EntityFrameworkCore;
using GB.Domain.Entities;

namespace GB.Infrastructure;

public class GreenBlanketDbContext : DbContext
{
    public GreenBlanketDbContext(DbContextOptions<GreenBlanketDbContext> options)
        : base(options) { }

    // This links your C# model to the database table
    public DbSet<ForumReport> ForumReports { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Explicitly map the entity to the exact table name in pgAdmin
        modelBuilder.Entity<ForumReport>().ToTable("report_forum");

        // 2. Ensure the primary key is mapped correctly
        modelBuilder.Entity<ForumReport>().HasKey(f => f.ReportForumId);

        // 3. The "Fixer" Mapping: Force exact lowercase column names for PostgreSQL
        modelBuilder.Entity<ForumReport>()
            .Property(f => f.ReportForumId)
            .HasColumnName("report_forum_id");

        modelBuilder.Entity<ForumReport>()
            .Property(f => f.ReportOptionsId)
            .HasColumnName("report_options_id");

        modelBuilder.Entity<ForumReport>()
            .Property(f => f.Name)
            .HasColumnName("name");

        modelBuilder.Entity<ForumReport>()
            .Property(f => f.Email)
            .HasColumnName("email");

        modelBuilder.Entity<ForumReport>()
            .Property(f => f.Message)
            .HasColumnName("message");

        modelBuilder.Entity<ForumReport>()
            .Property(f => f.Location)
            .HasColumnName("location");

        modelBuilder.Entity<ForumReport>()
            .Property(f => f.Date)
            .HasColumnName("date");
    }
}