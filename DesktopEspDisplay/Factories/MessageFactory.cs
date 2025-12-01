using DesktopEspDisplay.Constants;
using DesktopEspDisplay.Functions;
using DesktopEspDisplay.Models;

namespace DesktopEspDisplay.Factories;

public class MessageFactory
{
    private HardwareReader _hardwareReader;
    public MessageFactory()
    {
        _hardwareReader = new HardwareReader();
    }
    
    public Message CreateMessage(string action, uint delayMs = 50, string payload = "")
    {
        if(payload != "")
        {
            return new Message(DateTime.Now.AddMilliseconds(delayMs), action, payload);
        }
        return new Message(DateTime.Now.AddMilliseconds(delayMs), action, CreatePayloadByAction(action));
    }
    
    private string CreatePayloadByAction(string action)
    {
        switch (action)
        {
            case AppConst.SendCpuTemperatureCommand:
                return _hardwareReader.GetBestCpuTemperature() + "C";
            case AppConst.SendGpuTemperatureCommand:
                return _hardwareReader.GetGpuTemperature() + "C";
            case AppConst.SendWindowsVersionCommand:
                var a = Environment.OSVersion.VersionString.Split(' ');
                var b = a[1..];
                var u = b[2].Split('.')[0];
                var c = string.Join(" ", a[1], u);
                return c;
            case AppConst.SendRamUsageCommand:
            {
                var (used, total) = _hardwareReader.GetRamGB();
                return $"{used:0.0} / {total:0}GB";
            }
            case AppConst.SendUserInfoCommand:
                return Environment.UserName;
            default:
                return "";
        }
    }
}