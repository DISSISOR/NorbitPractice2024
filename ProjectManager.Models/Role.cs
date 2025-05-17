namespace ProjectManager.Models;
using System.ComponentModel.DataAnnotations;

public class Role
{
    [Key]
    public int Id { get; set; }
    public string Name { get; set; }

    public List<User> Users { get; set; } = new();

    public Role(int id, string name)
    {
	    this.Id = id;
	    this.Name = name;
    }
}
