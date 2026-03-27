using Microsoft.EntityFrameworkCore;
using GB.Domain.Entities;

namespace GB.Infrastructure;

public class GreenBlanketDbContext : DbContext
{
    public GreenBlanketDbContext(DbContextOptions<GreenBlanketDbContext> options)
        : base(options) { }

    // This links your C# model to the database table
    public DbSet<ForumReport> ForumReports { get; set; }
    public DbSet<UserAccount> UserAccounts { get; set; }

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
        modelBuilder.Entity<UserAccount>()
            .Property(u => u.Role)
            .HasColumnName("user_role");
    }
}