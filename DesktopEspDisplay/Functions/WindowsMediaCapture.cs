using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Windows.Media.Control;
using Windows.Storage.Streams;
using Buffer = System.Buffer;

namespace DesktopEspDisplay.Functions;

public class WindowsMediaCapture
{
    public byte[] CurrentAlbumArt { get; set; }
    
    public WindowsMediaCapture()
    {
        
    }
    
    public async Task<Tuple<string, byte[], byte[]>> GetAlbumArt()
    {

            var sessions = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var current = sessions.GetCurrentSession();

            if (current == null)
            {
                Console.WriteLine("No active media session found.");
                //todo send a default image?
                return new Tuple<string, byte[], byte[]>("No media", [], []);
            }

            var mediaInfo = await current.TryGetMediaPropertiesAsync();
            Console.WriteLine($"{mediaInfo.Title} - {mediaInfo.Artist}");

            var thumbRef = mediaInfo.Thumbnail;
            if (thumbRef == null)
            {
                Console.WriteLine("No album cover available.");
                //todo send a default image?
                return new Tuple<string, byte[], byte[]>($"{mediaInfo.Title} - {mediaInfo.Artist}", [], []);
            }

            // Read thumbnail bytes
            using var thumbStream = await thumbRef.OpenReadAsync();
            uint size = (uint)thumbStream.Size;
            byte[] imageData = new byte[size];
            using (var reader = new DataReader(thumbStream))
            {
                await reader.LoadAsync(size);
                reader.ReadBytes(imageData);
            }

            // Convert and resize into a guaranteed baseline RGB JPEG
            byte[] resized = ConvertToBaselineJpeg(imageData, 150, 150, 95);
            byte[] lenBytes = BitConverter.GetBytes(resized.Length);

            //combine all bytes to send (lenBytes + resized)

            // Save local preview
            //Directory.CreateDirectory("C:\\Temp");
            //File.WriteAllBytes("C:\\Temp\\thumb_preview.jpg", resized);
            //Console.WriteLine($"Preview saved to C:\\Temp\\thumb_preview.jpg ({resized.Length} bytes)");
            
            
            Console.WriteLine($"Thumb size ({resized.Length} bytes)");
            
            CurrentAlbumArt = resized;
            return new Tuple<string, byte[], byte[]>($"{mediaInfo.Title} - {mediaInfo.Artist}", lenBytes, resized);
    }
    
    // --- Helper to force a valid baseline RGB JPEG ---
    static byte[] ConvertToBaselineJpeg(byte[] inputBytes, int width, int height, long quality)
    {
        using (var msIn = new MemoryStream(inputBytes))
        using (var img = Image.FromStream(msIn))
        using (var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Black);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(img, 0, 0, width, height);

            using (var msOut = new MemoryStream())
            {
                var encoder = ImageCodecInfo.GetImageEncoders().First(c => c.MimeType == "image/jpeg");
                var encParams = new EncoderParameters(1);
                encParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                bmp.Save(msOut, encoder, encParams); // just baseline JPEG, no alpha, no profile
                return msOut.ToArray();
            }
        }
    }
}