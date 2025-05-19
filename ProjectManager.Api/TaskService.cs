namespace ProjectManager.Api;
using System.Text;

using Microsoft.EntityFrameworkCore;

using ProjectManager.Infrastructure;
using ProjectManager.Models;

public class TaskService
{
    private readonly ApplicationContext _ctx;

    public TaskService(ApplicationContext dbContext)
    {
        _ctx = dbContext;
    }

    public async Task<Task?> GetByIdAsync(int id)
    {
        return await _ctx.Set<Task>().FindAsync(id);
    }

    public async Task<List<Task>> GetAllAsync()
    {
        return await _ctx.Set<Task>().Include(t => t.Project).ToListAsync();
    }

    // public async Task<List<Task>> GetAllByUserAsync(int userId)
    // {
	//  #  var user = _ctx.Set<User>().FindAsync(userId).Result;
    //     if (user == null)
    //     {
    //         throw new ArgumentException("Не найден пользователь");
    //     }
    //     // return await _ctx.Set<Task>().Where(t => t.UserId == userId).ToListAsync();
    //     return await user..ToListAsync();
    // }

    public async System.Threading.Tasks.Task AddAsync(Task task)
    {
        _ctx.Set<Task>().Add(task);
        await _ctx.SaveChangesAsync();
    }

    public async System.Threading.Tasks.Task DeleteAsync(Task task)
    {
        if (task != null)
        {
            _ctx.Set<Task>().Remove(task);
            await _ctx.SaveChangesAsync();
        }
    }

    public async System.Threading.Tasks.Task DeleteByIdAsync(int id)
    {
        var task = await GetByIdAsync(id);
        if (task == null) throw new ArgumentException("Не найдена задача");
        _ctx.Set<Task>().Remove(task);
        await _ctx.SaveChangesAsync();
    }


    public int GetNextId()
    {
        return _ctx.Tasks.Any()
            ? _ctx.Tasks.Select(t => t.Id).Max() + 1
            : 1;
    }

    public async System.Threading.Tasks.Task Update(int id, string? name, bool? isActive)
    {
        var task = await _ctx.Tasks.FindAsync(id);
        if (task == null) throw new ArgumentException("Не найдена задача");

        if (name != null) task.Name = (string)name;
        // if (isActive != null) 
        if (isActive is bool someActive) {
	        task.IsActive = someActive;
        }

        await _ctx.SaveChangesAsync();
    }
}

