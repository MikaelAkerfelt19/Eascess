namespace Eascess_Application.Options;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string Host { get; set; } = "smtp.mailtrap.io";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "noreply@eascess.io";
    public string FromName { get; set; } = "Eascess";
}
