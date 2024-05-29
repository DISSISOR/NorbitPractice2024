namespace ProjectManager;

using ProjectRepository = IRepository<Project, int>;

public class Project
{
    public string Name { get; set; }
    public string Code { get; set; }
    public bool IsActive { get; set; }


    public Project(string name, string code, bool isActive = true)
    {
        this.Name = name;
        this.Code = code;
        this.IsActive = isActive;
    }
}
