namespace DesktopEspDisplay.Models;

public class Message
{
    public DateTime Timestamp { get; set; }
    public string Action { get; set; }
    public string Payload { get; set; }
    
    public Message(DateTime timestamp, string action, string payload)
    {
        Timestamp = timestamp;
        Action = action;
        Payload = payload;
    }
}