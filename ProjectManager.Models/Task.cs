namespace ProjectManager.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using System.Text.Json;
using System.Text.Json.Serialization;

public class Task
{
    public int Id { get; set; }
    public string? Name { get; set; }
    [Required]
    public Project Project { get; set; }
    [ForeignKey("Project")]
    public string ProjectCode { get; set; }

    [Required]
    public Role Role { get; set; }
    [ForeignKey("Role")]
    public int RoleId { get; set; }

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
    // public enum ReadyStatus {
	// 	[JsonPropertyName("todo")]
	// 	Todo,
	// 	[JsonPropertyName("in_progress")]
	// 	InProgress,
	// 	[JsonPropertyName("done")]
	// 	Done,
    // }

    // public ReadyStatus Status { get; set; }
}
