namespace ProjectManager.Api;
using System.Text;

using Microsoft.EntityFrameworkCore;

using ProjectManager.Infrastructure;
using ProjectManager.Models;

public class UserService
{
    private readonly ApplicationContext _ctx;

    public UserService(ApplicationContext dbContext)
    {
        _ctx = dbContext;
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        return await _ctx.Set<User>().FindAsync(id);
    }

    public async Task<List<User>> GetAllAsync()
    {
        return await _ctx.Set<User>().ToListAsync();
    }

    public async System.Threading.Tasks.Task AddAsync(User user)
    {
        _ctx.Set<User>().Add(user);
        await _ctx.SaveChangesAsync();
    }

    public async System.Threading.Tasks.Task DeleteAsync(User user)
    {
        if (user != null)
        {
            _ctx.Set<User>().Remove(user);
            await _ctx.SaveChangesAsync();
        }
    }

    public async System.Threading.Tasks.Task DeleteByIdAsync(int id)
    {
        var user = await GetByIdAsync(id);
        if (user == null) throw new ArgumentException("Не найден пользователь");
        _ctx.Set<User>().Remove(user);
        await _ctx.SaveChangesAsync();
    }


    public int GetNextId()
    {
        return _ctx.Set<User>().Any()
            ? _ctx.Users.Select(u => u.Id).Max() + 1
            : 1;
    }
}
