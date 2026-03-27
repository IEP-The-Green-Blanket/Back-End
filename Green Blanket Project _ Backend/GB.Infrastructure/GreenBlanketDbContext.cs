using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
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

        // Explicitly map the entity to the existing table name in pgAdmin
        modelBuilder.Entity<ForumReport>().ToTable("report_forum");

        // Ensure the primary key is mapped correctly if it's not standard "Id"
        modelBuilder.Entity<ForumReport>()
            .HasKey(f => f.ReportForumId);
    }
}