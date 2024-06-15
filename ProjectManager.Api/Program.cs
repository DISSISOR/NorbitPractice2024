using ProjectManager.Api;

using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;

using ProjectManager.Models;
using ProjectManager.Infrastructure;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.Development.json")
    .Build();

var connectionString = configuration.GetConnectionString("DefaultConnection");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ProjectService>();

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


tasksApi.MapGet("/", async (ApplicationContext ctx) => await ctx.Tasks.ToListAsync())
    .WithName("GetTasks")
    .WithOpenApi();

tasksApi.MapPost("/", async (string name, string projectCode, bool? isActive, ApplicationContext ctx) =>
{
    var project = await ctx.Projects.FindAsync(projectCode);
    if (project == null) return Results.NotFound("Project not found");

    var nextId = ctx.Tasks.Any()
        ? ctx.Tasks.Select(t => t.Id).Max() + 1
        : 1;
    var task = new ProjectManager.Models.Task {
        Id = nextId,
        Name = name,
        Project = project,
        IsActive = isActive ?? true,
    };
    ctx.Tasks.Add(task);
    await ctx.SaveChangesAsync();
    return Results.Created($"/tasks/{task.Id}", task);
})
    .WithName("CreateTask")
    .WithOpenApi();

tasksApi.MapGet("/{id:int}", async (int id, ApplicationContext ctx) =>
    await ctx.Tasks.FindAsync(id)
        is ProjectManager.Models.Task task
            ? Results.Ok(task)
            : Results.NotFound())
    .WithName("GetTaskByiD")
    .WithOpenApi();

tasksApi.MapDelete("/{id:int}", async (int id, ApplicationContext ctx) =>
{
    var task = await ctx.Tasks.FindAsync(id);
    if (task == null) return Results.NotFound();

    ctx.Tasks.Remove(task);
    await ctx.SaveChangesAsync();
    return Results.NoContent();
})
    .WithName("DeleteTask")
    .WithOpenApi();

tasksApi.MapPut("/{id:int}", async (int id, string? name, bool? isActive,  ApplicationContext ctx) =>
{
    var task = await ctx.Tasks.FindAsync(id);
    if (task == null) return Results.NotFound();

    if (name != null) task.Name = (string)name;
    if (isActive != null) task.IsActive = (bool)isActive!;

    await ctx.SaveChangesAsync();
    return Results.NoContent();
})
    .WithName("UpdateTask")
    .WithOpenApi();

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

entriesApi.MapGet("/", async (int? days, int? userId, ApplicationContext ctx) =>
{
    User? user = null;
    if (userId is int id)
    {
        user = await ctx.Users.FindAsync(id);
        if (user == null) return Results.NotFound();
    }
    if (days is int d)
    {
        var since = DateOnly.FromDateTime(DateTime.Now - new TimeSpan(d, 0, 0, 0));
        if (user is User u)
        {
            var res = await ctx.TimeEntries.Where(e => e.Date >= since && e.User == u).ToListAsync();
            return Results.Ok(res);
        } else
        {
            var res = await ctx.TimeEntries.Where(e => e.Date >= since).ToListAsync();
            return Results.Ok(res);
        }
    } else
    {
        if (user is User u)
        {
            var res = await ctx.TimeEntries.Where(e => e.User == u).ToListAsync();
            return Results.Ok(res);
        } else
        {
            var res = await ctx.TimeEntries.ToListAsync();
            return Results.Ok(res);
        }
    }
})
    .WithName("GetEntries")
    .WithOpenApi();

entriesApi.MapPost("/", async (DateOnly? date, TimeSpan time, string description, int taskId, int userId, ApplicationContext ctx) =>
{
    // TODO: проверка, что от пользователя поступило менее 24-х
    // часов проводок за день
    var task = await ctx.Tasks.FindAsync(taskId);
    if (task == null) return Results.NotFound("Task not found");
    var user = await ctx.Users.FindAsync(userId);
    if (user == null) return Results.NotFound("User not found");

    var nextId = ctx.TimeEntries.Any()
        ? ctx.TimeEntries.Select(e => e.Id).Max() + 1
        : 1;
    var entry = new TimeEntry
    {
        Id = nextId,
        Date = date ?? DateOnly.FromDateTime(DateTime.Now),
        Time = time,
        Description = description,
        Task = task,
        User = user,
    };
    ctx.TimeEntries.Add(entry);
    await ctx.SaveChangesAsync();
    return Results.Created($"/entries/{entry.Id}", entry);
})
    .WithName("CreateEntry")
    .WithOpenApi();

entriesApi.MapGet("/{id:int}", async (int id, ApplicationContext ctx) =>
{
    var entry = await ctx.TimeEntries.FindAsync(id);
    if (entry == null) return Results.NotFound();
    return Results.Ok(entry);
})
    .WithName("GetEntryById")
    .WithOpenApi();

entriesApi.MapDelete("/{id:int}", async (int id, ApplicationContext ctx) =>
{
    var entry = await ctx.TimeEntries.FindAsync(id);
    if (entry == null) return Results.NotFound();

    ctx.TimeEntries.Remove(entry);
    await ctx.SaveChangesAsync();
    return Results.NoContent();
})
    .WithName("DelteEntry")
    .WithOpenApi();

entriesApi.MapGet("/by_day_of_week/{day}", async (DayOfWeek day, int? userId,  ApplicationContext ctx) =>
{
    if (userId != null)
    {
        var user = await ctx.Users.FindAsync(userId);
        if (user is User u)
        {
            return Results.Ok(await ctx.TimeEntries.Where(e => e.User == u && e.Date.DayOfWeek == day).ToListAsync());
        } else
        {
            return Results.NotFound();
        }
    } else
    {
        return Results.Ok(await ctx.TimeEntries.Where(e => e.Date.DayOfWeek == day).ToListAsync());
    }
})
    .WithName("GetEntriesByDayOfWeek")
    .WithOpenApi();

entriesApi.MapGet("/by_day", async (DateOnly date, int? userId, ApplicationContext ctx) =>
{
    if (userId != null)
    {
        var user = await ctx.Users.FindAsync(userId);
        if (user is User u)
        {
            return Results.Ok(await ctx.TimeEntries.Where(e => e.User == u && e.Date == date).ToListAsync());
        } else
        {
            return Results.NotFound();
        }
    } else
    {
            return Results.Ok(await ctx.TimeEntries.Where(e => e.Date == date).ToListAsync());
    }
})
    .WithName("GetEntriesByDay")
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

