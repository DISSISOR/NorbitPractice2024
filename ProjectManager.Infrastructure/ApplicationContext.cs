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
            var admin = User.WithPassword(1, "admin", "admin123");
            admin.IsAdmin = true;
            Users.Add(admin);
            SaveChanges();
        }
        if (!Roles.Any()) {
	        var role = new Role(1, "base");
	        Roles.Add(role);
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
    public DbSet<Role> Roles => Set<Role>();
}

