namespace ProjectManager;

using TimeEntryRepository = IRepository<TimeEntry, int>;

public class TimeEntry
{
    public DateOnly Date { get; set; }
    public TimeSpan Time { get; private set; }
    public string Description { get; set; }
    public Task Task { get; set; }
    public User User { get; set; }

    public void AddTime(TimeSpan toAdd, TimeSpan spentToday)
    {
        if (toAdd + spentToday > new TimeSpan(1, 0, 0, 0))
        {
            throw new InvalidOperationException("Попытка добавить более 24-х часов проводок за день");
        }
        this.Time += toAdd;
    }

    public TimeEntry(Task task, User user, TimeSpan time, string desc, DateOnly? date)
    {
        if (!task.IsActive) {
            throw new InvalidOperationException("Попытка создать проводку по неактивному заданию");
        }
        this.Task = task;
        this.Date = date ??  DateOnly.FromDateTime(DateTime.Now);
        this.Description = desc;
        this.Time = time;
        this.User = user;
    }
}
