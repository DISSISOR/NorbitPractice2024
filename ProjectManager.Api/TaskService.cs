namespace ProjectManager.Api;
using System.Text;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

using ProjectManager.Infrastructure;
using ProjectManager.Models;

public class TaskService
{
    private readonly ApplicationContext _ctx;
    private ProjectService _projectService;

    public TaskService(ApplicationContext dbContext, ProjectService projectService)
    {
        _ctx = dbContext;
        _projectService = projectService;
    }

    public async Task<TaskDTO?> GetByIdAsync(int id)
    {
        return await TaskDTO.FromTask(await _ctx.Set<Task>().FindAsync(id), _projectService);
    }

    public async Task<List<TaskDTO>> GetAllAsync()
    {
        // var tasks = _ctx.Set<Task>().AsEnumerable();;
        // var dtos = tasks.Select(t => TaskDTO.FromTask(t, _projectService).Result).ToList();
        // return dtos;
        List<TaskDTO> tasks = await _ctx.Set<Task>().Join(_ctx.Set<Project>(),
             t => t.ProjectCode, p => p.Code,
             (t, p) => new TaskDTO {
                Id = t.Id,
                Name = t.Name,
                ProjectName = p.Name,
                ProjectCode = p.Code,
                IsActive = t.IsActive,
             }
        ).ToListAsync();
        return tasks;
    }

    public async Task<List<Task>> GetAllByUserAsync(int userId)
    {
        if (_ctx.Set<User>().FindAsync(userId).Result == null)
        {
            throw new ArgumentException("Не найден пользователь");
        }
        return await _ctx.Set<Task>().Where(t => t.UserId == userId).ToListAsync();
    }

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
        var task = await _ctx.Set<Task>().FindAsync(id);
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
        if (isActive != null) task.IsActive = (bool)isActive;

        await _ctx.SaveChangesAsync();
    }
}

public class TaskDTO
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [Required]
    [JsonPropertyName("project_code")]
    public string ProjectCode { get; set; }
    [JsonPropertyName("name_project")]
    public string ProjectName { get; set; }

    [JsonPropertyName("active")]
    public bool IsActive { get; set; }

    public static async Task<TaskDTO> FromTask(Task task, ProjectService projectService)
    {
        var project = await projectService.GetByCodeAsync(task.ProjectCode);
        return new TaskDTO
        {
            Id = task.Id,
            Name = task.Name,
            ProjectName = project.Name,
            ProjectCode = project.Code,
            IsActive = task.IsActive,
        };
    }

    // public static async Task<IEnumerable<TaskDTO>> FromTasks(IEnumerable<Task> tasks, ProjectService projectService)
    // {

    // }
}
