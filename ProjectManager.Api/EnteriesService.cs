using Microsoft.EntityFrameworkCore;
using ProjectManager.Infrastructure;
using ProjectManager.Models;

namespace ProjectManager.Api.Properties;

public class EnteriesService
{
    private readonly ApplicationContext _ctx;

    public EnteriesService(ApplicationContext dbContext)
    {
        _ctx = dbContext;
    }

    public async Task<TimeEntry?> GetByIdAsync(int id)
    {
        return await _ctx.Set<TimeEntry>().FindAsync(id);
    }

    public async Task<List<TimeEntry>> GetAllAsync()
    {
        return await _ctx.Set<TimeEntry>().ToListAsync();
    }

    public async System.Threading.Tasks.Task AddAsync(TimeEntry time_entry)
    {
        if( !time_entry.Task.IsActive ) throw new ArgumentException("Проводка по неактивной задаче");
        _ctx.Set<TimeEntry>().Add(time_entry);
        await _ctx.SaveChangesAsync();
    }

    public async System.Threading.Tasks.Task DeleteAsync(TimeEntry time_entry)
    {
        if (time_entry != null)
        {
            _ctx.Set<TimeEntry>().Remove(time_entry);
            await _ctx.SaveChangesAsync();
        }
    }

    public async System.Threading.Tasks.Task DeleteByIdAsync(int id)
    {
        var time_entry = await GetByIdAsync(id);
        if (time_entry == null) throw new ArgumentException("Не найдена проводка");
        _ctx.Set<TimeEntry>().Remove(time_entry);
        await _ctx.SaveChangesAsync();
    }


    public int GetNextId()
    {
        return _ctx.TimeEntries.Any()
            ? _ctx.TimeEntries.Select(t => t.Id).Max() + 1
            : 1;
    }

    // public async System.Threading.Tasks.Task Update(int id, int user_id, int task_id, 
    //     DateOnly date, TimeSpan? time, string? description)
    // {
    //     var time_entry = await _ctx.TimeEntries.FindAsync(id);
    //     if(user_id != time_entry.UserId) throw new ArgumentException("Редактирование проводок другого пользовател недоступно");
    //     if (time_entry == null) throw new ArgumentException("Не найдена проводка");

    //     if (date != null) time_entry.Date = (DateOnly)date;
    //     if (time != null) time_entry.Time = (TimeSpan)time;
    //     if (description != null) time_entry.Description = (string)description;
        
    //     var task = await _ctx.Tasks.FindAsync(task_id);
    //     if (task != null)
    //     {
    //         if (task.IsActive)
    //         {
    //             time_entry.Task = task;
    //             time_entry.TaskId = task_id;
    //         } else if (task_id != time_entry.TaskId) throw new ArgumentException("Проводка по неактивной задаче");
    //     }

    //     await _ctx.SaveChangesAsync();
    // }
}
