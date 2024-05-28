namespace ProjectManager;

public class Task
{
    public string Name { get; set; }
    public Project Project { get; set; }
    public bool IsActive { get; set; }

    public Task(Project proj, string name, bool isActive = true)
    {
        this.Name = name;
        this.Project = proj;
        this.IsActive = isActive;
    }
}
