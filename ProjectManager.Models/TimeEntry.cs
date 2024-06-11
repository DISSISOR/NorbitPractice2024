namespace ProjectManager.Models;
using System.ComponentModel.DataAnnotations;

public class TimeEntry
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }
    public TimeSpan? Time { get; set; }
    public string? Description { get; set; }
    [Required]
    public Task Task { get; set; }
    [Required]
    public User User { get; set; }

    // public TimeEntry(Task task, User user, TimeSpan time, string desc, DateOnly? date)
    // {
    //     if (!task.IsActive) {
    //         throw new InvalidOperationException("Попытка создать проводку по неактивной задаче");
    //     }
    //     this.Task = task;
    //     this.Date = date ?? DateOnly.FromDateTime(DateTime.Now);
    //     this.Description = desc;
    //     this.Time = time;
    //     this.User = user;
    // }
}
