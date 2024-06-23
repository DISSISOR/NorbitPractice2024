using ProjectManager.Api;

using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using ProjectManager.Api.Properties;
using ProjectManager.Models;
using ProjectManager.Infrastructure;
using Task = ProjectManager.Models.Task;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.Development.json")
    .Build();

var connectionString = configuration.GetConnectionString("DefaultConnection");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<TaskService>();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddCors();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.MapType<DateOnly>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "date"
    });
    options.MapType<TimeSpan>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "time"
    });
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
	{
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = AuthOptions.ISSUER,
            ValidateAudience = true,
            ValidAudience = AuthOptions.AUDIENCE,
            ValidateLifetime = true,
            IssuerSigningKey = AuthOptions.GetSymmetricSecurityKey(),
            ValidateIssuerSigningKey = true,
        };
	}
);
builder.Services.AddAuthorization();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseCors(builder => builder.AllowAnyOrigin());

var projectsApi = app.MapGroup("/projects").WithOpenApi()
    .WithTags("Projects");
var tasksApi = app.MapGroup("/tasks").WithOpenApi()
    .WithTags("Tasks");
var usersApi = app.MapGroup("/users").WithOpenApi()
    .WithTags("Users");
var loginApi = app.MapGroup("/login").WithOpenApi()
    .WithTags("Auth");
var entriesApi = app.MapGroup("/entries").WithOpenApi()
    .WithTags("Entries");

loginApi.MapPost("/{username}", (string username) =>
{
    var claims = new List<Claim> {new Claim(ClaimTypes.Name, username) };
    var jwt = new JwtSecurityToken(
            issuer: AuthOptions.ISSUER,
            audience: AuthOptions.AUDIENCE,
            claims: claims,
            expires: DateTime.UtcNow.Add(TimeSpan.FromMinutes(2)),
            signingCredentials: new SigningCredentials(AuthOptions.GetSymmetricSecurityKey(), SecurityAlgorithms.HmacSha256));
    return new JwtSecurityTokenHandler().WriteToken(jwt);
})
    .WithName("Login")
    .WithOpenApi();

usersApi.MapGet("/", async (UserService userService) => await userService.GetAllAsync())
    .WithName("GetUsers")
    .WithOpenApi();

usersApi.MapPost("/", async (string name, UserService userService) =>
{
    var nextId = userService.GetNextId();
    var user = new User(nextId, name);
    await userService.AddAsync(user);
    return Results.Created($"/users/{user.Id}", user);
})
    .WithName("AddUser")
    .WithOpenApi();

usersApi.MapGet("/{id:int}", async (int id, UserService userService) =>
    await userService.GetByIdAsync(id)
        is User user
            ? Results.Ok(user)
            : Results.NotFound())
    .WithName("GetUserByiD")
    .WithOpenApi();

usersApi.MapDelete("/{id:int}", async (int id, UserService userService) =>
{
    try
    {
        await userService.DeleteByIdAsync(id);
        return Results.NoContent();
    } catch(ArgumentException ex)
    {
        return Results.NotFound(ex.Message);
    }
})
    .WithName("DeleteUser")
    .WithOpenApi();

projectsApi.MapGet("/", async (ProjectService projectService) => await projectService.GetAllAsync())
    .WithName("GetProjects")
    .WithOpenApi();

projectsApi.MapPost("/", async (string name, bool? isActive, ProjectService projectService) =>
{
	var nextCode = projectService.GetNextCode();
    var project = new Project(name, nextCode, isActive ?? true);
    await projectService.AddAsync(project);
    return Results.Created($"/projects/{project.Code}", project);
})
    .WithName("AddProject")
    .WithOpenApi();

projectsApi.MapGet("/{code:regex([0-9]+)}", async (string code, ProjectService projectService) =>
{
    var project = await projectService.GetByCodeAsync(code);
    return project != null
        ? Results.Ok(project)
        : Results.NotFound();
})
    .WithName("GetProjectByCode")
    .WithOpenApi();

projectsApi.MapDelete("/{code:regex([0-9]+)}", async (string code, ProjectService projectService) =>
{
    try
    {
        await projectService.DeleteByCodeAsync(code);
        return Results.NoContent();
    } catch (ArgumentException ex)
    {
        return Results.NotFound(ex.Message);
    }
})
    .WithName("DeleteProject")
    .WithOpenApi();

projectsApi.MapPut("/{code:regex([0-9]+)}", async (string code, string? name, bool? isActive, ProjectService projectService) =>
{
    try
    {
        await projectService.Update(code, name, isActive);
        return Results.NoContent();
    } catch (ArgumentException ex)
    {
        return Results.NotFound(ex.Message);
    }
})
    .WithName("UpdateProject")
    .WithOpenApi();

// FIXME: здесь стоит использовать TaskService и ProjectService вместо сырой БД.
// Первого на момент написания комментария не существует
projectsApi.MapGet("/{code:regex([0-9]+)}/tasks", async (string code, ApplicationContext ctx) =>
{
    var proj = await ctx.Projects.FindAsync(code);
    if (proj == null) return Results.NotFound();
    return Results.Ok(
        await ctx.Tasks.Where(t => t.Project == proj).ToListAsync()
    );
})
    .WithName("GetProjectTasks")
    .WithOpenApi();


tasksApi.MapGet("/", async (TaskService taskService) => await taskService.GetAllAsync())
    .WithName("GetTasks")
    .WithOpenApi();

tasksApi.MapPost("/", async (string name, string projectCode, bool? isActive, TaskService taskService, ProjectService projectService) =>
{
    Project project;
    try
    {
        project = await projectService.GetByCodeAsync(projectCode);
    } catch (ArgumentException ex)
    {
        return Results.NotFound(ex.Message);
    }

    var nextId = taskService.GetNextId();
    var task = new ProjectManager.Models.Task {
        Id = nextId,
        Name = name,
        Project = project,
        IsActive = isActive ?? true,
    };
    await taskService.AddAsync(task);
    return Results.Created($"/tasks/{task.Id}", task);
})
    .WithName("CreateTask")
    .WithOpenApi();

tasksApi.MapGet("/{id:int}", async (int id, TaskService taskService) =>
    await taskService.GetByIdAsync(id)
        is ProjectManager.Models.Task task
            ? Results.Ok(task)
            : Results.NotFound())
    .WithName("GetTaskByiD")
    .WithOpenApi();

tasksApi.MapDelete("/{id:int}", async (int id, TaskService taskService) =>
{
    try
    {
        await taskService.DeleteByIdAsync(id);
        return Results.NoContent();
    } catch (ArgumentException ex)
    {
        return Results.NotFound(ex.Message);
    }
})
    .WithName("DeleteTask")
    .WithOpenApi();

tasksApi.MapPut("/{id:int}", async (int id, string? name, bool? isActive,  TaskService taskService) =>
{
    try
    {
        await taskService.Update(id, name, isActive);
        return Results.NoContent();
    } catch (ArgumentException ex)
    {
        return Results.NotFound(ex.Message);
    }
})
    .WithName("UpdateTask")
    .WithOpenApi();

// FIXME: использовать другие сервисы
tasksApi.MapGet("/{id:int}/entries", async (int id, ApplicationContext ctx) =>
{
    var task = await ctx.Tasks.FindAsync(id);
    if (task == null) return Results.NotFound();
    return Results.Ok(
        await ctx.TimeEntries.Where(e => e.Task == task).ToListAsync()
    );
})
    .WithName("GetTaskEntries")
    .WithOpenApi();

entriesApi.MapGet("/", async (EnteriesService enteriesService) =>
await enteriesService.GetAllAsync())
    .WithName("GetEntries")
    .WithOpenApi();

entriesApi.MapPost("/", async (DateOnly? date, TimeSpan time, 
        string description, Task task, User user, EnteriesService enteriesService) =>
{
    // проверка, что от пользователя поступило менее 24-х часов проводок за день
    var allEntries = await enteriesService.GetAllAsync();
    var entryDate = date ?? DateOnly.FromDateTime(DateTime.Now);
    
    // Подсчитываем общее количество времени для проводок с той же датой
    var totalHoursForDate = allEntries
        .Where(e => e.Date == entryDate && e.UserId == user.Id)
        .Select(e => e.Time)
        .Aggregate(TimeSpan.Zero, (sum, next) => 
            sum.Add((TimeSpan)next!)).TotalHours;
    if (totalHoursForDate + time.TotalHours > 24)
    {
        return Results.BadRequest("Превышено максимальное количество часов за день.");
    }

    var nextId = enteriesService.GetNextId();
    var entry = new TimeEntry
    {
        Id = nextId,
        Date = date ?? DateOnly.FromDateTime(DateTime.Now),
        Time = time,
        Description = description,
        Task = task,
        TaskId = task.Id,
        User = user,
        UserId = user.Id
    };
    
    await enteriesService.AddAsync(entry);
    return Results.Created($"/entries/{entry.Id}", entry);
})
    .WithName("CreateEntry")
    .WithOpenApi();

entriesApi.MapGet("/{id:int}", async (int id, EnteriesService enteriesService) =>
(await enteriesService.GetByIdAsync(id))
    is TimeEntry entry
    ? Results.Ok() 
    : Results.NotFound())
    .WithName("GetEntryById")
    .WithOpenApi();

entriesApi.MapDelete("/{id:int}", async (int id, EnteriesService entry) =>
{
    try
    {
        await entry.DeleteByIdAsync(id);
        return Results.NoContent();
    }
    catch (ArgumentException ex)
    {
        return Results.NotFound(ex.Message);
    }
})
    .WithName("DelteEntry")
    .WithOpenApi();

entriesApi.MapGet("/by_day_of_week/{day}", async (DayOfWeek day, int? userId,  EnteriesService entry) =>
{
    var allEntries = await entry.GetAllAsync();
    if (userId != null)
    {
        try
        {
            return Results.Ok(allEntries
                .Where(e => e.UserId == userId 
                            && e.Date.DayOfWeek == day));
        }
        catch (ArgumentException ex)
        {
            return Results.NotFound();
        }
    }
    return Results.Ok(allEntries
        .Where(e => e.Date.DayOfWeek == day));
})
    .WithName("GetEntriesByDayOfWeek")
    .WithOpenApi();

entriesApi.MapGet("/by_day", async (DateOnly date, int? userId, EnteriesService entry) =>
{
    var allEntries = await entry.GetAllAsync();
    if (userId != null)
    {
        try
        {
            return Results.Ok(allEntries
                .Where(e => e.UserId == userId 
                            && e.Date == date));
        }
        catch (ArgumentException ex)
        {
            return Results.NotFound();
        }
    }
    return Results.Ok(allEntries
        .Where(e => e.Date == date));
})
    .WithName("GetEntriesByDay")
    .WithOpenApi();

tasksApi.MapPut("/{id:int}", async (int id, int user_id, int task_id,
        DateOnly date, TimeSpan? time, string? description,  EnteriesService entry) =>
    {
        try
        {
            await entry.Update(id, user_id, task_id, date, time, description);
            return Results.NoContent();
        } catch (ArgumentException ex)
        {
            return Results.NotFound(ex.Message);
        }
    })
    .WithName("UpdateEntry")
    .WithOpenApi();


app.Run();

public class AuthOptions
{
    public const string ISSUER = "KSU";
    public const string AUDIENCE = "PlaceHolderAudience";
    const string KEY = "mysupersecret_secretsecretsecretkey!123";
    public static SymmetricSecurityKey GetSymmetricSecurityKey() =>
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(KEY));
}

