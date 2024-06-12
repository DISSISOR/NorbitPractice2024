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

    public bool IsActive { get; set; }
}
