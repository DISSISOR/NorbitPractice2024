using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace ProjectManager;

public class User
{
    private int _id; //{ get; set; }
    public string Name { get; set; }
    private bool ExistingUser = false;

    public User(int id, string name)
    {
        Name = name;
    }

    private void Authentication( string password)
    {
        try
        {
            _id = Database_actions.GetId(Name);
            password = HashPassword(password); // password.GetHashCode().ToString();
            if(password != Database_actions.GetPassword(_id))
                throw new InvalidOperationException("Incorrect password or username.");
            ExistingUser = true;
        }
        catch (Exception e)
        {
            ExistingUser = false;
            throw new InvalidOperationException("Authorization failed.");
        }
    }
    
    private string HashPassword(string password)
    {
        using (var sha256 = SHA256.Create())
        {
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }
            return builder.ToString();
        }
    }
}

