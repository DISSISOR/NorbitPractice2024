namespace ProjectManager.Models;

using System.Text;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

public class User
{

    public int Id { get; set; }
    public string Name { get; set; }
    [JsonIgnore]
    public string Hash { get; set;}
    public Role Role { get; set; }

    public User(int id, string name, string hash)
        : this(id, name, hash, Role.User){
    }

    public User(int id, string name, string hash, Role role)
    {
        this.Id = id;
        this.Name = name;
        this.Hash = hash;
        this.Role = role;
    }

    public string RoleAsString()
    {
        return Role switch {
            Role.Admin => "admin",
            Role.User => "user",
        };
    }

    public static User WithPassword(int id, string name, string password, Role role)
    {
        var hash = GenHash(name, password);
        return new User(id, name, hash, role);
    }

    public static User WithPassword(int id, string name, string password)
    {
        return WithPassword(id, name, password, Role.User);
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

public enum Role
{
    Admin,
    User,
}
