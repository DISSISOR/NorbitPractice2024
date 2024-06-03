using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
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
builder.Services.AddSwaggerGen();
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
var usersApi = app.MapGroup("/users").WithOpenApi()
    .WithTags("Users");
var loginApi = app.MapGroup("/login").WithOpenApi()
    .WithTags("Auth");

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

app.Run();

public class AuthOptions
{
    public const string ISSUER = "KSU";
    public const string AUDIENCE = "PlaceHolderAudience";
    const string KEY = "mysupersecret_secretsecretsecretkey!123";
    public static SymmetricSecurityKey GetSymmetricSecurityKey() =>
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(KEY));
}

