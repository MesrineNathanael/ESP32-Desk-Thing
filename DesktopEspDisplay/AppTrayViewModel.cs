using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopEspDisplay.Constants;
using DesktopEspDisplay.Factories;
using DesktopEspDisplay.Functions;
using DesktopEspDisplay.Models;
using NAudio.Wave;

namespace DesktopEspDisplay;

public class AppTrayViewModel : ObservableObject
{

    private SerialPort? _serialPort;
    private bool _isServiceStarted = false;
    private static bool _isSendingImage = false;
    
    public Config Configuration { get; set; }

    public AudioWaveCapture AudioCapture { get; set; } = new AudioWaveCapture();
    
    public MessageFactory MessageFactory { get; set; } = new MessageFactory();
    
    public WindowsMediaCapture WindowsMediaCapture { get; set; } = new WindowsMediaCapture();
    
    public Task RunnerThread { get; set; }

    public bool IsServiceStarted
    {
        get => _isServiceStarted;
        set
        {
            if (value == _isServiceStarted) return;
            _isServiceStarted = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ServiceInfo));
            OnPropertyChanged(nameof(StartStopServiceButtonText));
            OnPropertyChanged(nameof(StartStopServiceCommand));
        }
    }

    public bool AskStopService { get; set; } = false;

    public bool ConnectedToDevice => _serialPort?.IsOpen ?? false;
    
    public List<Message> MessageQueues { get; set; } = new();
    
    public string DeviceInfo => ConnectedToDevice ? "Connected to device" : "Not connected to device";
    
    public string ServiceInfo => IsServiceStarted ? "Service is running" : "Service is stopped";
    
    public string StartStopServiceButtonText => IsServiceStarted ? "Stop Service" : "Start Service";
    
    public AppTrayViewModel()
    {
        //Load configuration
        Configuration = Config.LoadConfig();
        
        RunnerThread = Task.Factory.StartNew(Runner);
        AudioCapture.Capture.DataAvailable += CaptureOnDataAvailable;
    }

    private void CaptureOnDataAvailable(object? sender, WaveInEventArgs e)
    {
        //check if there is only message in queue
        try
        {
            if (MessageQueues.Any(x => x.Action == AppConst.SendSoundWaveCommand))
            {
                return;
            }
        }catch(Exception ex)
        {
            //
        }


        var data = AudioCapture.GetAudio(e);
        AddMessageToQueue(MessageFactory.CreateMessage(AppConst.SendSoundWaveCommand, 20, string.Join(",", data)));
    }

    private void Runner()
    {
        AudioCapture.Capture.StartRecording();
        
        while (!AskStopService)
        {
            try
            {
                //try to connect if not connected
                if (!ConnectedToDevice)
                {
                    OpenSerialPort();
                    if(!ConnectedToDevice)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }
                    Thread.Sleep(100);
                    //add events to queue
                    AddMessageToQueue(MessageFactory.CreateMessage(AppConst.SendUserInfoCommand));
                    AddMessageToQueue(MessageFactory.CreateMessage(AppConst.SendWindowsVersionCommand));
                    AddMessageToQueue(MessageFactory.CreateMessage(AppConst.SendCpuTemperatureCommand));
                    AddMessageToQueue(MessageFactory.CreateMessage(AppConst.SendGpuTemperatureCommand));
                    AddMessageToQueue(MessageFactory.CreateMessage(AppConst.SendRamUsageCommand));
                }
                
                var now = DateTime.Now;
                var toConsume = MessageQueues.Where(a => a.Timestamp <= now).ToList();
                foreach (var message in toConsume)
                {
                    ConsumeMessage(message);
                    MessageQueues.Remove(message);
                    Thread.Sleep(50);
                }
                
                //mark service as started
                IsServiceStarted = true;
                
                OnPropertyChanged(nameof(DeviceInfo));
                AutoMessageToQueue();
                Thread.Sleep(20);
            }
            catch (Exception e)
            {
                //AskStopService = true;
                Console.WriteLine($"Error in Runner: {e.Message}");
                Thread.Sleep(100);
            }
        }
        
        if (_serialPort is { IsOpen: true })
        {
            _serialPort.Close();
        }
        IsServiceStarted = false;
        AudioCapture.Capture.StopRecording();
        AskStopService = false;
        
        OnPropertyChanged(nameof(DeviceInfo));
    }
    
    private void AutoMessageToQueue()
    {
        if (MessageQueues.All(x => x.Action != AppConst.SendCpuTemperatureCommand))
        {
            AddMessageToQueue(MessageFactory.CreateMessage(AppConst.SendCpuTemperatureCommand, Configuration.CpuTemperatureIntervalMs));
        }
        else if (MessageQueues.All(x => x.Action != AppConst.SendGpuTemperatureCommand))
        {
            AddMessageToQueue(MessageFactory.CreateMessage(AppConst.SendGpuTemperatureCommand, Configuration.GpuTemperatureIntervalMs));
        }
        else if (MessageQueues.All(x => x.Action != AppConst.SendRamUsageCommand))
        {
            AddMessageToQueue(MessageFactory.CreateMessage(AppConst.SendRamUsageCommand, Configuration.RamUsageIntervalMs));
        }
        else if (MessageQueues.All(x => x.Action != AppConst.SendImageCommand))
        {
            AddMessageToQueue(MessageFactory.CreateMessage(AppConst.SendImageCommand, Configuration.ImageSendIntervalMs));
        }
        else if (MessageQueues.All(x => x.Action != AppConst.SendSoundTitleCommand))
        {
            AddMessageToQueue(MessageFactory.CreateMessage(AppConst.SendSoundTitleCommand, Configuration.SoundTitleIntervalMs));
        }
    }
    
    private void OpenSerialPort()
    {
        if (_serialPort is not { IsOpen: true })
        {
            _serialPort = new SerialPort(Configuration.SerialPortName, Configuration.SerialBaudRate);
            _serialPort.Open();
            Thread.Sleep(200);
            
            if (!_serialPort.IsOpen) return;
        }
        
        OnPropertyChanged(nameof(DeviceInfo));
    }
    
    private void CloseSerialPort()
    {
        if (_serialPort is { IsOpen: true })
        {
            _serialPort.Close();
            _serialPort.Dispose();
            Thread.Sleep(200);
        }
        
        OnPropertyChanged(nameof(DeviceInfo));
    }

    private void AddMessageToQueue(Message message)
    {
        MessageQueues.Add(message);
    }
    private TaskCompletionSource<bool> _okReceived;
    private void ConsumeMessage(Message message)
    {
        if (_isSendingImage)
            return;

        if (message is { Action: AppConst.SendImageCommand, Payload: "" })
        {
            Task.Factory.StartNew(() =>
            {
                _isSendingImage = true;
                var albumArt = WindowsMediaCapture.GetAlbumArt();
                if (albumArt.Result.Item2.Length == 0) return;
                var ok = false;
                var tries = 0;
                while (!ok)
                {
                    SendToDevice(AppConst.SendImageCommand, albumArt.Result.Item2, albumArt.Result.Item3);

                    Thread.Sleep(100);
                    var read = _serialPort!.ReadExisting();
                    if (read.Contains("[IMG] Image drawn successfully"))
                    {
                        ok = true;
                        Console.WriteLine("OK !");
                    }

                    //Console.WriteLine(read);
                    tries++;
                    if (tries > 3)
                    {
                        Console.WriteLine("Failed to receive OK after image send.");
                        break;
                    }
                }

                _isSendingImage = false;
            });
        }
        else if (message is { Action: AppConst.SendSoundTitleCommand, Payload: "" })
        {
            Task.Factory.StartNew(() =>
            {
                var albumArt = WindowsMediaCapture.GetAlbumArt();
                if (albumArt.Result.Item1 != string.Empty)
                {
                    var title = albumArt.Result.Item1.Length > 35 ? albumArt.Result.Item1[..35] : albumArt.Result.Item1;
                    SendToDevice(AppConst.SendSoundTitleCommand, title);
                }
            });
        }
        else
        {
            SendToDevice(message.Action, message.Payload ?? "");
        }
    }
    
    private void SendToDevice(string action, string data)
    {
        if (ConnectedToDevice)
        {
            _serialPort?.WriteLine($"{action}{data}\n");
        }
    }

    private void SendToDevice(string action, byte[] header, byte[] data)
    {
        if (ConnectedToDevice)
        {
            _serialPort?.Write(action);
            _serialPort?.Write(header, 0, 4);
            _serialPort?.Write(data, 0, data.Length);
            _serialPort?.Write("\n");
        }
    }
    
    private MainWindow? GetMainWindow()
    {
        return Application.Current.MainWindow as MainWindow;
    }

    public ICommand ShowWindowCommand
    {
        get
        {
            return new RelayCommand(() =>
            {
                Task.Factory.StartNew(() =>
                {
                    MessageBox.Show("oui", "oui");
                });
            });
        }
    }
    
    public ICommand EditConfigCommand
    {
        get
        {
            return new RelayCommand(() =>
            {
                Task.Factory.StartNew(() =>
                {
                    using var process = new Process();
                    process.StartInfo.FileName = "explorer";
                    var strExeFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    var strWorkPath = Path.GetDirectoryName(strExeFilePath)!;
                    process.StartInfo.Arguments = "\"" + Path.Join(strWorkPath, Config.ConfigFilePath) + "\"";
                    process.Start();
                });
            });
        }
    }
    
    public ICommand StartStopServiceCommand
    {
        get
        {
            return new RelayCommand(() =>
            {
                if (IsServiceStarted)
                {
                    AskStopService = true;
                }
                else
                {
                    RunnerThread = Task.Factory.StartNew(Runner);
                }
            });
        }
    }
    
    public ICommand ExitApplicationCommand
    {
        get
        {
            return new RelayCommand(() =>
            {
                Application.Current.Shutdown();
            });
        }
    }
}
