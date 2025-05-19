namespace ProjectManager.Models;

using System.Text;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class User
{
	[JsonPropertyName("id")]
    public int Id { get; set; }
	[JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonIgnore]
    public string Hash { get; set;}
    // public Role Role { get; set; }
    public ICollection<Role> Roles { get; set; } = new List<Role>();
	[JsonPropertyName("is_admin")]
	public bool IsAdmin { get; set; }
	[JsonPropertyName("is_manager")]
	public bool IsManager { get; set; }

    public User(int id, string name, string hash)
    {
        this.Id = id;
        this.Name = name;
        this.Hash = hash;
    }

    public static User WithPassword(int id, string name, string password)
    {
        var hash = GenHash(name, password);
        return new User(id, name, hash);
    }

	public string PermAsString() {
		if (IsAdmin) return "admin";
		if (IsManager) return "manager";
		return "user";
	}

    public static string GenHash(string name, string password)
    {
        var toHash = name + '#' + password;
        var algorithm = SHA256.Create();
        var bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(toHash));
        var sb = new StringBuilder();
        foreach (byte b in bytes)
        {
            sb.Append(b.ToString());
        }
        return sb.ToString();
    }
}

