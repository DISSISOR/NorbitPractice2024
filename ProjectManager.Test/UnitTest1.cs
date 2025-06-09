namespace ProjectManager.Test;

using Microsoft.EntityFrameworkCore;

using ProjectManager.Api;
using ProjectManager.Infrastructure;
using ProjectManager.Models;

public class UnitTest1
{
    [Fact]
    public async System.Threading.Tasks.Task Add()
    {
		var options = new DbContextOptionsBuilder<ApplicationContext>()
			.UseInMemoryDatabase(databaseName: "Test1Db")
			.Options;
	    await using (var ctx = new ApplicationContext(options)) {
			var srv = new ProjectService(ctx);
			var p = new Project("проект1", srv.GetNextCode());
			await srv.AddAsync(p);

			var projects = await srv.GetAllAsync();
			Assert.Equal(1, projects.Count());
			Assert.Equal("проект1", projects[0].Name);
			Assert.Equal("1", projects[0].Code);
			Assert.Equal(true, projects[0].IsActive);

			p = new Project("проект2", srv.GetNextCode());
			await srv.AddAsync(p);
			p = await srv.GetByCodeAsync("2");
			Assert.NotNull(p);
			Assert.Equal("проект2", p.Name);
			Assert.Equal("2", p.Code);
	    }
    }
    [Fact]
    public async System.Threading.Tasks.Task Detete()
    {
		var options = new DbContextOptionsBuilder<ApplicationContext>()
			.UseInMemoryDatabase(databaseName: "Test2Db")
			.Options;
	    await using (var ctx = new ApplicationContext(options)) {
			var srv = new ProjectService(ctx);
			var p = new Project("проект1", srv.GetNextCode());
			await srv.AddAsync(p);
			var ps = await srv.GetAllAsync();
			Assert.NotEmpty(ps);
			await srv.DeleteByCodeAsync("1");
			ps = await srv.GetAllAsync();
			Assert.Empty(ps);
	    }
    }

    [Fact]
    public async System.Threading.Tasks.Task Update()
    {
		var options = new DbContextOptionsBuilder<ApplicationContext>()
			.UseInMemoryDatabase(databaseName: "Test3Db")
			.Options;
	    await using (var ctx = new ApplicationContext(options)) {
			var srv = new ProjectService(ctx);
			var p = new Project("проект1", srv.GetNextCode());
			await srv.AddAsync(p);
			var ps = await srv.GetAllAsync();
			Assert.NotEmpty(ps);
			Assert.Equal("проект1", ps[0].Name);
			Assert.Equal("1", ps[0].Code);
			Assert.Equal(true, ps[0].IsActive);
			await srv.Update("1", "проект2", null);
			Assert.Equal("проект2", ps[0].Name);
			Assert.Equal("1", ps[0].Code);
			Assert.Equal(true, ps[0].IsActive);
			await srv.Update("1", "проект1", false);
			Assert.Equal("проект1", ps[0].Name);
			Assert.Equal("1", ps[0].Code);
			Assert.Equal(false, ps[0].IsActive);
	    }
    }
}
