namespace ProjectManager.Infrastructure;

using Microsoft.EntityFrameworkCore;

using ProjectManager.Models;

public class ApplicationContext: DbContext
{
    public ApplicationContext(DbContextOptions<ApplicationContext> opts)
        : base(opts) {
        Database.EnsureCreated();
        if (!Users.Any())
        {
            var admin = User.WithPassword(1, "admin", "admin", Role.Admin);
            Users.Add(admin);
            SaveChanges();
        }
    }
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<User>()
            .HasIndex(u => u.Name)
            .IsUnique();
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Task> Tasks => Set<Task>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();

}
