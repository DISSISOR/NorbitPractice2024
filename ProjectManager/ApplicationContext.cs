namespace ProjectManager;
using Microsoft.EntityFrameworkCore;

public class ApplicationContext: DbContext
{
    public DbSet<User>? Users { get; set; }
    public DbSet<Project>? Projects { get; set; }
    public DbSet<Task>? Tasks { get; set; }
    public DbSet<TimeEntry>? TimeEntries { get; set; }
    private string _connectionString;
    public ApplicationContext(string connectionString)
    {
        _connectionString = connectionString;
        Database.EnsureCreated();
    }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(_connectionString);
    }
}
