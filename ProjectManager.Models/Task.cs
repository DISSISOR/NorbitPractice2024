namespace ProjectManager.Models;
using System.ComponentModel.DataAnnotations;

public class Task
{
    public int Id { get; set; }
    public string? Name { get; set; }
    [Required]
    public Project Project { get; set; }
    public bool IsActive { get; set; }
}
