using System.IO.Ports;
using DesktopEspDisplay.Constants;
using DesktopEspDisplay.Models;

namespace DesktopEspDisplay.Functions;

public class MessageConsumer
{
    private readonly SerialPort _serialPort;

    private readonly WindowsMediaCapture _windowsMediaCapture;
    
    private static bool _isSendingImage = false;
    
    public bool ConnectedToDevice => _serialPort?.IsOpen ?? false;
    
    private string _sendedMusicTitleThumb = "";
    
    public MessageConsumer(SerialPort serialPort, WindowsMediaCapture windowsMediaCapture)
    {
        _serialPort = serialPort;
        _windowsMediaCapture = windowsMediaCapture;
    }
    
    public void ConsumeMessage(Message message)
    {
        if (_isSendingImage)
            return;

        if (message is { Action: AppConst.SendImageCommand, Payload: "" })
        {
            Task.Factory.StartNew(() =>
            {
                var albumArt = _windowsMediaCapture.GetAlbumArt();
                var title = albumArt.Result.Item1;
                if (_sendedMusicTitleThumb == title) return;
                if (albumArt.Result.Item2.Length == 0) return;
                _isSendingImage = true;
                var ok = false;
                var tries = 0;
                while (!ok)
                {
                    SendToDevice(AppConst.SendImageCommand, albumArt.Result.Item2, albumArt.Result.Item3);

                    Thread.Sleep(100);
                    var read = _serialPort!.ReadExisting();
                    if (read.Contains("[IMG] Image drawn successfully"))
                    {
                        _sendedMusicTitleThumb = title;
                        ok = true;
                        Console.WriteLine("[IMG] OK !");
                    }

                    //Console.WriteLine(read);
                    tries++;
                    if (tries > 3)
                    {
                        Console.WriteLine("[IMG] Failed to receive OK after image send.");
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
                var albumArt = _windowsMediaCapture.GetAlbumArt();
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
}