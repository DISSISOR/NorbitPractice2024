using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;

using ProjectManager;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.Development.json")
    .Build();

var connectionString = configuration.GetConnectionString("DefaultConnection");

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
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
builder.Services.AddDbContext<ApplicationContext>(options => options.UseNpgsql(connectionString));
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

projectsApi.MapGet("/", async (ApplicationContext ctx) => await ctx.Projects.ToListAsync())
    .WithName("GetProjects")
    .WithOpenApi();

projectsApi.MapPost("/", async (string name, bool? isActive, ApplicationContext ctx) =>
{
    // TODO: работает только с 32-битными числами. Обобщить для
    // кодов произвольного размера.

    var nextKey = (ctx.Projects.AsEnumerable().Select(
  		 p => Int32.Parse(p.Code)).Max() + 1).ToString();
    var project = new Project(name, nextKey, isActive ?? true);
    ctx.Projects.Add(project);
    await ctx.SaveChangesAsync();
    return Results.Created($"/projects/{project.Code}", project);
})
    .WithName("AddProject")
    .WithOpenApi();

projectsApi.MapGet("/{code:regex([0-9]+)}", async (string code, ApplicationContext ctx) =>
    await ctx.Projects.FindAsync(code)
        is Project proj
            ? Results.Ok(proj)
            : Results.NotFound())
    .WithName("GetProjectByCode")
    .WithOpenApi();

projectsApi.MapGet("/{code:regex([0-9]+)}/tasks", async (string code, ApplicationContext ctx) =>
{
    var proj = await ctx.Projects.FindAsync(code);
    if (proj == null) return Results.NotFound();
    return Results.Ok(
        ctx.Tasks.Where(t => t.Project == proj).ToListAsync()
    );
})
    .WithName("GetProjectTasks")
    .WithOpenApi();

usersApi.MapGet("/", async (ApplicationContext ctx) => await ctx.Users.ToListAsync())
    .WithName("GetUsers")
    .WithOpenApi();

usersApi.MapPost("/", async (string name, ApplicationContext ctx) =>
{
    var nextId = ctx.Users.Select(u => u.Id).Max() + 1;
    var user = new User(nextId, name);
    ctx.Users.Add(user);
    await ctx.SaveChangesAsync();
    return Results.Created($"/users/{user.Id}", user);
})
    .WithName("AddUser")
    .WithOpenApi();

usersApi.MapGet("/{id:int}", async (int id, ApplicationContext ctx) =>
    await ctx.Users.FindAsync(id)
        is User user
            ? Results.Ok(user)
            : Results.NotFound())
    .WithName("GetUserByiD")
    .WithOpenApi();

tasksApi.MapGet("/", async (ApplicationContext ctx) => await ctx.Tasks.ToListAsync())
    .WithName("GetTasks")
    .WithOpenApi();

tasksApi.MapPost("/", async (string name, int projectId, bool? isActive, ApplicationContext ctx) =>
{
    var project = await ctx.Projects.FindAsync(projectId);
    if (project == null) return Results.NotFound("Project not found");

    var nextId = ctx.Tasks.Select(t => t.Id).Max() + 1;
    var task = new ProjectManager.Task {
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
        is ProjectManager.Task task
            ? Results.Ok(task)
            : Results.NotFound())
    .WithName("GetTaskByiD")
    .WithOpenApi();

entriesApi.MapGet("/", async (int? days, ApplicationContext ctx) =>
{
    if (days is int d)
    {
        var since = DateOnly.FromDateTime(DateTime.Now - new TimeSpan(d, 0, 0, 0));
        return await ctx.TimeEntries.Where(e => e.Date >= since).ToListAsync();
    } else
    {
        return await ctx.TimeEntries.ToListAsync();
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

    var nextId = ctx.TimeEntries.Select(e => e.Id).Max() + 1;
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

