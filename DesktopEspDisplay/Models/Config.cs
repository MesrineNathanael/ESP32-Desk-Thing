using System.IO;

namespace DesktopEspDisplay.Models;

[Serializable]
public class Config 
{
    [NonSerialized]
    public static readonly string ConfigFilePath = "config.json";
    
    public string SerialPortName { get; set; } = "COM3";
    public int SerialBaudRate { get; set; } = 921600;
    public uint ImageSendIntervalMs { get; set; } = 10000;
    public uint SoundTitleIntervalMs { get; set; } = 5000;
    public uint CpuTemperatureIntervalMs { get; set; } = 3000;
    public uint GpuTemperatureIntervalMs { get; set; } = 3000;
    public uint RamUsageIntervalMs { get; set; } = 5000;
    public uint AudioVisualizerIntervalMs { get; set; } = 10;
    
    public static Config LoadConfig()
    {
        if (!File.Exists(ConfigFilePath))
        {
            var defaultConfig = new Config();
            SaveConfig(defaultConfig);
            return defaultConfig;
        }

        var json = File.ReadAllText(ConfigFilePath);
        return System.Text.Json.JsonSerializer.Deserialize<Config>(json) ?? new Config();
    }
    
    public static void SaveConfig(Config config)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(ConfigFilePath, json);
    }
}