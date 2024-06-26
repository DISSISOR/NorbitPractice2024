namespace ProjectManager.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Task
{
    public int Id { get; set; }
    public string? Name { get; set; }
    [Required]
    public Project Project { get; set; }
    [ForeignKey("Project")]
    public string ProjectCode { get; set; }

    [Required]
    public User User { get; set; }
    [ForeignKey("User")]
    public int UserId { get; set; }

    public bool IsActive { get; set; }

    // public Task(int id, string name, Project project, User user, bool isActive = true)
    // {
    //     this.Id = id;
    //     this.Name = name;
    //     this.Project = project;
    //     this.ProjectCode = project.Code;
    //     this.User = user;
    //     this.UserId = user.Id;
    //     this.IsActive = isActive;
    // }
}
