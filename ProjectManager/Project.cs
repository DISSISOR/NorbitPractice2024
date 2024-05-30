namespace ProjectManager;
using System.ComponentModel.DataAnnotations;

public class Project
{
    [Key]
    public string Code { get; set; }
    public string Name { get; set; }
    public bool IsActive { get; set; }

    public Project(string name, string code, bool isActive = true)
    {
        this.Name = name;
        this.Code = code;
        this.IsActive = isActive;
    }
}
