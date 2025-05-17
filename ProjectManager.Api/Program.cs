using ProjectManager.Api;

using System.Text;
using System.Web;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

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
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter a valid token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "Bearer"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type=ReferenceType.SecurityScheme,
                    Id="Bearer"
                }
            },
            new string[]{}
        }
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

app.Use(async (context, next) => {
    var request = context.Request;
    var path = request.Path;
    if (path.StartsWithSegments("/login") && !path.StartsWithSegments("/login/register")) {
        await next.Invoke();
        return;
    }
    if (!context.User.Identity.IsAuthenticated) {
        await context.ChallengeAsync();
        return;
    }
    await next.Invoke();
});

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
var rolesApi = app.MapGroup("/roles").WithOpenApi()
    .WithTags("Roles");

// loginApi.MapPost("/register", (string username, string password) =>
// {
// })
//     .WithName("Register")
//     .WithOpenApi();

loginApi.MapPost("/register", [Authorize(Roles="admin")] async (string name, string password, bool? isAdmin, bool? isManager, UserService userService) =>
{
    var nextId = userService.GetNextId();
    // var role = Role.User;
    // var isUserAdmin = false;
    // if (isAdmin ?? false)
    // {
    //     isUserAdmin ;
    // }
    var user = User.WithPassword(nextId, name, password);
    if (isAdmin ?? false)
    {
        user.IsAdmin = true;
    }
    if (isManager ?? false)
    {
        user.IsManager = true;
    }
    await userService.AddAsync(user);
    return Results.Created($"/users/{user.Id}", user);
})
    .WithName("Register")
    .WithOpenApi();

loginApi.MapPost("/{username}", async (string username, string password, UserService userService) =>
{
    var isValid = await userService.VerifyAsync(username, password);
    if (!isValid) return Results.Unauthorized();
    var user = await userService.GetByNameAsync(username);
    var claims = new List<Claim> {
        new Claim(ClaimTypes.Name, username),
        new Claim(ClaimsIdentity.DefaultRoleClaimType, user.PermAsString()),
        new Claim(ClaimTypes.Hash, user.Hash),
    };
    var jwt = new JwtSecurityToken(
            issuer: AuthOptions.ISSUER,
            audience: AuthOptions.AUDIENCE,
            claims: claims,
            expires: DateTime.UtcNow.Add(TimeSpan.FromMinutes(10)),
            signingCredentials: new SigningCredentials(AuthOptions.GetSymmetricSecurityKey(), SecurityAlgorithms.HmacSha256));
    var response = new
    {
        access_token = new JwtSecurityTokenHandler().WriteToken(jwt),
        id = user.Id,
        name = user.Name,
        role = user.PermAsString(),
    };
    return Results.Json(response);
})
    .WithName("Login")
    .WithOpenApi();

usersApi.MapGet("/", [Authorize(Roles="admin")] async (UserService userService) => await userService.GetAllAsync())
    .WithName("GetUsers")
    .WithOpenApi();


usersApi.MapGet("/{id:int}", async (int id, UserService userService) =>
    await userService.GetByIdAsync(id)
        is User user
            ? Results.Ok(user)
            : Results.NotFound())
    .WithName("GetUserByiD")
    .WithOpenApi();

usersApi.MapDelete("/{id:int}", [Authorize(Roles="admin")] async (int id, UserService userService) =>
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

tasksApi.MapPost("/", async (string name, string projectCode, int roleId, ProjectManager.Models.Task.ReadyStatus? status, TaskService taskService, ProjectService projectService, UserService userService) =>
{
    var project = await projectService.GetByCodeAsync(projectCode);
    if (project == null) return Results.NotFound("Проект не найден");
    // var user = await userService.GetByIdAsync(userId);
    // if (user == null) return Results.NotFound("Пользователь не найден");

    var nextId = taskService.GetNextId();
    var task = new ProjectManager.Models.Task {
        Id = nextId,
        Name = name,
        Project = project,
        Status = status ?? ProjectManager.Models.Task.ReadyStatus.InProgress,
    };
    // var task = new ProjectManager.Models.Task(nextId, name, project, user, isActive ?? true);
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
    .WithName("GetTaskById")
    .WithOpenApi();

// tasksApi.MapGet("/by_user", async (int userId, TaskService taskService) =>
// {
    // try
    // {
        // var tasks = await taskService.GetAllByUserAsync(userId);
        // return Results.Ok(tasks);
    // } catch (ArgumentException ex)
    // {
        // return Results.NotFound(ex.Message);
    // }
// })
    // .WithName("GetTasksByUserId")
    // .WithOpenApi();

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

tasksApi.MapPut("/{id:int}", async (int id, string? name, ProjectManager.Models.Task.ReadyStatus? status,  TaskService taskService) =>
{
    try
    {
        await taskService.Update(id, name, status);
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
            var entries = await EntriesByUser(ctx, u.Id);
            var res = entries.Where(e => e.Date >= since).ToList();
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
            var res = await EntriesByUser(ctx, u.Id);
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
    var task = await ctx.Tasks.FindAsync(taskId);
    if (task == null) return Results.NotFound("Task not found");
    var user = await ctx.Users.FindAsync(userId);
    if (user == null) return Results.NotFound("User not found");

    var _date = date ?? DateOnly.FromDateTime(DateTime.Now);
    var entries = await EntriesByUser(ctx, userId);
    var sum_hours = entries
        .Where(e => e.Date == _date)
        .Aggregate(TimeSpan.Zero, (sum, next) => sum + (TimeSpan)next.Time);

    if (sum_hours + time > new TimeSpan(1, 0, 0, 0))
    {
        return Results.BadRequest("Сумма проводок превышает 24 часа за один день");
    }

    var nextId = ctx.TimeEntries.Any()
        ? ctx.TimeEntries.Select(e => e.Id).Max() + 1
        : 1;
    var entry = new TimeEntry
    {
        Id = nextId,
        Date = _date,
        Time = time,
        Description = description,
        Task = task,
        User = user,
        UserId = user.Id,
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
            var entries = await EntriesByUser(ctx, u.Id);
            return Results.Ok(entries.Where(e => e.Date.DayOfWeek == day).ToList());
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
            var entries = await EntriesByUser(ctx, u.Id);
            return Results.Ok(entries.Where(e => e.Date == date).ToList());
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

rolesApi.MapGet("/", async (ApplicationContext ctx) =>
{
	return Results.Ok(await ctx.Set<Role>().ToListAsync());
})
    .WithName("GetRoles")
    .WithOpenApi();

rolesApi.MapPost("/", async (string name, ApplicationContext ctx) =>
{
    var nextId = ctx.Roles.Any()
        ? ctx.Roles.Select(r => r.Id).Max() + 1
        : 1;
	var role = new Role(nextId, name);
	ctx.Roles.Add(role);
	await ctx.SaveChangesAsync();
	return Results.Created($"/roles/{role.Id}", role);
})
    .WithName("CreateRole")
    .WithOpenApi();

app.Run();

static async System.Threading.Tasks.Task<List<TimeEntry>> EntriesByUser(ApplicationContext ctx, int userId)
{
    var entries = ctx.TimeEntries;
    // var tasks = ctx.Tasks;
    return await entries.Where(e => e.UserId == userId)
       .ToListAsync();
}

public class AuthOptions
{
    public const string ISSUER = "KSU";
    public const string AUDIENCE = "PlaceHolderAudience";
    const string KEY = "mysupersecret_secretsecretsecretkey!123";
    public static SymmetricSecurityKey GetSymmetricSecurityKey() =>
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(KEY));
}
