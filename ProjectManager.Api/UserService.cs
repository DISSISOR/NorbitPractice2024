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

    public async Task<User?> GetByNameAsync(string name)
    {
        return await _ctx.Set<User>().SingleOrDefaultAsync(u => u.Name == name);
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

    public async Task<bool> VerifyAsync(string name, string password)
    {
        var user = await GetByNameAsync(name);
        if (user == null) return false;
        var hash = User.GenHash(name, password);
        return user.Hash == hash;
    }

    public async System.Threading.Tasks.Task Update(int id, string? name, string? password)
    {
        var user = await _ctx.Users.FindAsync(id);
        if (user == null) throw new ArgumentException("Не найден пользователь");

        if (name != null) user.Name = (string)name;
		if (password is string newPassword) {
			var hash = User.GenHash(user.Name, newPassword);
			user.Hash = hash;
		}
        await _ctx.SaveChangesAsync();
    }
}
