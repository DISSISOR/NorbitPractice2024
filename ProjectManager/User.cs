namespace ProjectManager;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; }

    public User(int id, string name)
    {
		this.Id = id;
		this.Name = name;
    }
}
