namespace ProjectManager.Api;
using System.Text;

using Microsoft.EntityFrameworkCore;

using ProjectManager.Infrastructure;
using ProjectManager.Models;

public class ProjectService
{
    private readonly ApplicationContext _ctx;

    public ProjectService(ApplicationContext dbContext)
    {
        _ctx = dbContext;
    }

    public async Task<Project?> GetByCodeAsync(string code)
    {
        return await _ctx.Set<Project>().FindAsync(code);
    }

    public async Task<List<Project>> GetAllAsync()
    {
        return await _ctx.Set<Project>().ToListAsync();
    }

    public async System.Threading.Tasks.Task AddAsync(Project project)
    {
        _ctx.Set<Project>().Add(project);
        await _ctx.SaveChangesAsync();
    }

    public async System.Threading.Tasks.Task DeleteAsync(Project project)
    {
        if (project != null)
        {
            _ctx.Set<Project>().Remove(project);
            await _ctx.SaveChangesAsync();
        }
    }

    public async System.Threading.Tasks.Task DeleteByCodeAsync(string code)
    {
        var project = await GetByCodeAsync(code);
        if (project == null) throw new ArgumentException("Не найден пользователь");
        _ctx.Set<Project>().Remove(project);
        await _ctx.SaveChangesAsync();
    }


    public string GetNextCode()
    {
        // TODO: работает только с 32-битными числами. Обобщить для
        // кодов произвольного размера.
        var nextKey = _ctx.Projects.Any()
            ? (_ctx.Projects.AsEnumerable()
               .Select(
                p => Int32.Parse(p.Code))
                .Max() + 1)
            : 1;
        return nextKey.ToString();
    }
}

