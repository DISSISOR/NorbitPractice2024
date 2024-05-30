namespace ProjectManager;
using Microsoft.EntityFrameworkCore;

public class ApplicationContext: DbContext
{
    public ApplicationContext(DbContextOptions<ApplicationContext> opts)
        : base(opts) {
        Database.EnsureCreated();
    }
    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Task> Tasks => Set<Task>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
}
