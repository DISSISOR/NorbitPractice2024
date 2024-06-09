using ProjectManager;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.Development.json")
    .Build();

var connectionString = configuration.GetConnectionString("DefaultConnection");

// var ctx = new ApplicationContext(connectionString);
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<ApplicationContext>(options => options.UseNpgsql(connectionString));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var projectsApi = app.MapGroup("/projects").WithOpenApi();

projectsApi.MapGet("/", async (ApplicationContext ctx) => await ctx.Projects.ToListAsync()) 
.WithName("GetProjects")
.WithOpenApi();

projectsApi.MapPost("/", async (Project project, ApplicationContext ctx) =>
{
    ctx.Projects.Add(project);
    await ctx.SaveChangesAsync();
    return Results.Created($"/projects/{project.Code}", project);
})
.WithName("AddProject")
.WithOpenApi();

app.Run();

