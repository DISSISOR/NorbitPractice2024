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
builder.Services.AddScoped<EnteriesService>();

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
builder.Services.AddCors(options =>
{
	options.AddPolicy("AllowFrontend", policy =>
	{
	    policy.WithOrigins("http://localhost:5100", "http://localhost:5173")
	          .WithMethods("GET", "POST", "PUT", "DELETE")
	          .WithHeaders("Content-Type", "Authorization") // Explicitly allow Auth header
	          .AllowCredentials();
	});
});

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
// app.UseCors(builder => builder.AllowAnyOrigin());
app.UseCors("AllowFrontend");

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
    var existingUser = await userService.GetByNameAsync(name);
    if (existingUser != null) {
	    return Results.Conflict($"Пользователь с именем {name} уже существует");
    }
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
        new Claim("uid", user.Id.ToString())
    };
    var jwt = new JwtSecurityToken(
            issuer: AuthOptions.ISSUER,
            audience: AuthOptions.AUDIENCE,
            claims: claims,
            expires: DateTime.UtcNow.Add(TimeSpan.FromDays(10)),
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

usersApi.MapGet("/", async (UserService userService) => await userService.GetAllAsync())
    .WithName("GetUsers")
    .WithOpenApi();

usersApi.MapGet("/me", [Authorize] async (ClaimsPrincipal userPrincipal, ApplicationContext ctx) => {
	 var uid = userPrincipal.GetUserId();
	 return Results.Ok(uid);
})
    .WithName("GetMe")
    .WithOpenApi();

usersApi.MapGet("/{id:int}", async (int id, ApplicationContext ctx) => {
	    var res =  await ctx.Users
	        .Where(u => u.Id == id)
	        .Select(u => new
	        {
	            Id = u.Id,
	            Name = u.Name,
	            IsManager = u.IsManager,
	            IsAdmin = u.IsAdmin,
	            Roles = u.Roles.Select(r => new
	            {
	                Id = r.Id,
	                Name = r.Name
	            }).ToList()
	        })
	        .AsNoTracking()
	        .FirstOrDefaultAsync();
        if (res == null) return Results.NotFound("User not found");
        return Results.Ok(res);
})
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

usersApi.MapPut("/{id:int}", [Authorize(Roles="admin")] async (int id, string? name, string? password, UserService userService) =>
{
    try
    {
        await userService.Update(id, name, password);
        return Results.NoContent();
    } catch (ArgumentException ex)
    {
        return Results.NotFound(ex.Message);
    }
})
    .WithName("UpdateUser")
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

tasksApi.MapGet("/", async (TaskService taskService, ApplicationContext ctx) => {
	var task = await ctx.Tasks.Include(t => t.Role).Include(t => t.Project).ToListAsync();
	if (task == null) {
		return Results.NotFound("Задача не найдена");
	}
	return Results.Ok(task);
})
    .WithName("GetTasks")
    .WithOpenApi();

tasksApi.MapGet("/by_role/{roleId:int}", async (int roleId, TaskService taskService, ApplicationContext ctx) => {
	var tasks = await ctx.Tasks.Where(t => t.RoleId == roleId).ToListAsync();
	return Results.Ok(tasks);
})
    .WithName("GetTasksByRole")
    .WithOpenApi();

tasksApi.MapGet("/by_user/{userId:int}", async (int userId, TaskService taskService, UserService userService, ApplicationContext ctx) => {
	var user = await userService.GetByIdAsync(userId);
	if (user == null) {
		return Results.NotFound("Пользователь не найден");
	}
	var tasks = await ctx.Tasks.Include(t => t.Role).Where(t => t.Role.Users.Any(u => u.Id == userId))
		.ToListAsync();
	return Results.Ok(tasks);
})
    .WithName("GetTasksByUser")
    .WithOpenApi();

tasksApi.MapGet("/by_day/entries", async (DateOnly date, TaskService taskService, ApplicationContext ctx) => {
	var entries = ctx.TimeEntries.Include(e => e.Task).Include(e => e.User).Where(e => e.Date == date);
	var result = await entries.Select(e =>
		new {
			id = e.Task.Id,
			name = e.Task.Name,
			isActive = e.Task.IsActive,
			user = new {
				name = e.User.Name,
				id = e.User.Id,
				entryId = e.Id,
				entryTime = e.Time,
			}
		}
	).ToListAsync();
	var entryList = await ctx.TimeEntries.Where(e => e.Date == date).ToListAsync();
	var sum = entryList.Aggregate(TimeSpan.Zero, (sum, next) => sum + (TimeSpan)next.Time);
	return Results.Ok(new {
		results = result,
		sum = sum
	});
})
    .WithName("GetTaskSummaryByDate")
    .WithOpenApi();

tasksApi.MapPost("/", async (string name, string projectCode, int roleId, bool? isActive, TaskService taskService, ProjectService projectService, UserService userService, ApplicationContext ctx) =>
{
    var project = await projectService.GetByCodeAsync(projectCode);
    if (project == null) return Results.NotFound("Проект не найден");
    // var user = await userService.GetByIdAsync(userId);
    // if (user == null) return Results.NotFound("Пользователь не найден");

	var role = await ctx.Roles.FindAsync(roleId);
	if (role == null) return Results.NotFound("Роль не найдена");

    var nextId = taskService.GetNextId();
    var task = new ProjectManager.Models.Task {
        Id = nextId,
        Name = name,
        Project = project,
        IsActive = isActive ?? true,
		Role = role,
		RoleId = role.Id,
    };
    // var task = new ProjectManager.Models.Task(nextId, name, project, user, isActive ?? true);
    await taskService.AddAsync(task);
    // await ctx.SaveChangesAsync();
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

entriesApi.MapGet("/", async (int? days, int? userId, ApplicationContext ctx, EnteriesService enteriesService) =>
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
            // var res = await ctx.TimeEntries.Where(e => e.Date >= since).ToListAsync();
            var res = await enteriesService.GetAllAsync();
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
            //var res = await ctx.TimeEntries.ToListAsync();
            var res = await enteriesService.GetAllAsync();
            return Results.Ok(res);
        }
    }
})
    .WithName("GetEntries")
    .WithOpenApi();

entriesApi.MapPost("/", [Authorize] async (DateOnly? date, ClaimsPrincipal userClaim, TimeSpan time, string description, int taskId, ApplicationContext ctx) =>
{
	var userIdStr = userClaim.FindFirstValue("uid");

	if (!int.TryParse(userIdStr, out int userId))
    {
        return Results.Unauthorized();
    }

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
    var entry = await ctx.TimeEntries.Include(e => e.Task).FirstOrDefaultAsync(e => e.Id == id);
    if (entry == null) return Results.NotFound();
    return Results.Ok(entry);
})
    .WithName("GetEntryById")
    .WithOpenApi();

entriesApi.MapPut("/{id:int}", async (int id, ClaimsPrincipal userPrincipal,
                                                  DateOnly? date, int? taskId,
                                                  TimeSpan? time, string? desc,
                                                  EnteriesService enteriesService, ApplicationContext ctx) => {
	var uid = userPrincipal.GetUserId();
	var entry = await enteriesService.GetByIdAsync(id);
	if (entry == null) {
		return Results.NotFound("Не найдена проводка");
	}
	if (uid != entry.UserId) {
		return Results.Forbid();
	}
    try
    {
        await enteriesService.Update(id, uid, taskId, date, time, desc);
        return Results.NoContent();
    } catch (ArgumentException ex)
    {
        return Results.NotFound(ex.Message);
    }
})
    .WithName("UpdateEntry")
    .WithOpenApi();

entriesApi.MapDelete("/{id:int}", async (int id, ApplicationContext ctx) =>
{
    var entry = await ctx.TimeEntries.FindAsync(id);
    if (entry == null) return Results.NotFound();

    ctx.TimeEntries.Remove(entry);
    await ctx.SaveChangesAsync();
    return Results.NoContent();
})
    .WithName("DeleteEntry")
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

rolesApi.MapDelete("/{id:int}", async (int id, ApplicationContext ctx) =>
{
    var role = await ctx.Roles.FindAsync(id);
    if (role == null) return Results.NotFound();

    ctx.Roles.Remove(role);
    await ctx.SaveChangesAsync();
    return Results.NoContent();
})
    .WithName("DeleteRole")
    .WithOpenApi();

rolesApi.MapGet("/{id:int}/users", async (int id, ApplicationContext ctx) =>
{
	var result = await ctx.Roles
		.Where(r => r.Id == id)
		.Select(r => new
		{
		    Users = r.Users.Select(u => new
		    {
		        u.Id,
		        u.Name
		        // Other properties as needed
		    })
		})
		.AsNoTracking()
		.FirstOrDefaultAsync();

	return result is null ? Results.NotFound() : Results.Ok(result.Users);
})
    .WithName("GetRoleUsers")
    .WithOpenApi()
    .Produces<List<User>>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);


rolesApi.MapPut("/{roleId:int}/adduser/{userId:int}", async (int roleId, int userId, ApplicationContext ctx) =>
{
    var user = await ctx.Users.FindAsync(userId);
    if (user == null) return Results.NotFound();

    var role = await ctx.Roles
    	.Include(r => r.Users)
    	.FirstOrDefaultAsync(r => r.Id == roleId);
    if (role == null) return Results.NotFound();
    if (role.Users.Contains(user)) {
	    return Results.Conflict("User already has this role");
    }

    role.Users.Add(user);
	await ctx.SaveChangesAsync();
    return Results.Ok($"Role {role.Name} added to user {user.Name}");

})
    .WithName("AddUserRole")
    .WithOpenApi()
    .Produces(StatusCodes.Status404NotFound);

app.Run();

static async System.Threading.Tasks.Task<List<TimeEntry>> EntriesByUser(ApplicationContext ctx, int userId)
{
    var entries = ctx.TimeEntries;
    return await entries.Where(e => e.UserId == userId)
       .Include(e => e.Task)
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


public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var userIdStr = principal.FindFirstValue("uid");
        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
        {
            throw new UnauthorizedAccessException("Invalid user ID claim");
        }

        return userId;
    }

    public static bool TryGetUserId(this ClaimsPrincipal principal, out int userId)
    {
        userId = default;
        var userIdStr = principal.FindFirstValue("uid");
        return !string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out userId);
    }
}
