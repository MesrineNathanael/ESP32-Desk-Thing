using Windows.Devices.Sensors;
using Microsoft.VisualBasic.Devices;

namespace DesktopEspDisplay.Functions;

using LibreHardwareMonitor.Hardware;
using System.Runtime.InteropServices;

public class HardwareReader
{
    private Computer _computer;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);
    
    public HardwareReader()
    {
        _computer = new Computer()
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true
        };
        _computer.Open();
    }

    public float? GetBestCpuTemperature()
    {
        float? best = null;

        foreach (var hardware in _computer.Hardware)
        {
            // CPU SENSOR
            if (hardware.HardwareType == HardwareType.Cpu ||
                hardware.HardwareType == HardwareType.Motherboard ||
                hardware.HardwareType == HardwareType.SuperIO ||
                hardware.HardwareType == HardwareType.EmbeddedController)
            {
                hardware.Update();
                foreach (var sub in hardware.SubHardware)
                    sub.Update();

                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Temperature)
                    {
                        // ignore zero readings
                        if (sensor.Value.HasValue && sensor.Value.Value > 0)
                        {
                            // pick the highest — CPU temp is almost always the hottest sensor
                            if (best == null || sensor.Value > best)
                                best = sensor.Value;
                        }
                    }
                }
            }
        }

        return best;
    }
    public float? GetGpuTemperature()
    {
        float? temp = null;

        foreach (var hardware in _computer.Hardware)
        {
            if (hardware.HardwareType == HardwareType.GpuNvidia ||
                hardware.HardwareType == HardwareType.GpuAmd ||
                hardware.HardwareType == HardwareType.GpuIntel)
            {
                hardware.Update();

                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Temperature &&
                        sensor.Name.Contains("GPU Core"))
                    {
                        temp = sensor.Value;
                    }
                }
            }
        }
        return temp;
    }

    public (float usedGB, float totalGB) GetRamGB()
    {
        MEMORYSTATUSEX mem = new MEMORYSTATUSEX();
        GlobalMemoryStatusEx(mem);

        float totalGB = mem.ullTotalPhys / (1024f * 1024f * 1024f);
        float usedGB  = (mem.ullTotalPhys - mem.ullAvailPhys) / (1024f * 1024f * 1024f);

        return (usedGB, totalGB);
    }
}
