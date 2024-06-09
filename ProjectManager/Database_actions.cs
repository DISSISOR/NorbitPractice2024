namespace ProjectManager;

public class Database_actions
{
    public static int GetId(string Name)
    {
        try
        {
            //here is code to get Id from DB by Name
            return 1;
        }
        catch (Exception e)
        {
            throw new InvalidOperationException("Database is not available.");
        }
        
    }

    public static string GetPassword(int Id)
    {
        try
        {
            //here is code to get hash-code for password (stored in DB) by Id
            return "";
        }
        catch (Exception e)
        {
            throw new InvalidOperationException("Database is not available.");
        }
        
    }
    
}