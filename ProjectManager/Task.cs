namespace ProjectManager;

public class Task
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public Project? Project { get; set; }
    public bool IsActive { get; set; }
}
