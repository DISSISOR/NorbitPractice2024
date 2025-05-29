namespace ProjectManager.Test;

using Microsoft.EntityFrameworkCore;

using ProjectManager.Api;
using ProjectManager.Infrastructure;
using ProjectManager.Models;

public class UnitTest1
{
    [Fact]
    public async System.Threading.Tasks.Task Test1()
    {
		var options = new DbContextOptionsBuilder<ApplicationContext>()
			.UseInMemoryDatabase(databaseName: "TestDb")
			.Options;
	    await using (var ctx = new ApplicationContext(options)) {
			var srv = new ProjectService(ctx);
			var p = new Project("проект1", srv.GetNextCode());
			await srv.AddAsync(p);

			var projects = await srv.GetAllAsync();
			Assert.Equal(1, projects.Count());
			Assert.Equal("проект1", projects[0].Name);
			Assert.Equal("1", projects[0].Code);

			p = new Project("проект2", srv.GetNextCode());
	    }
    }
}
