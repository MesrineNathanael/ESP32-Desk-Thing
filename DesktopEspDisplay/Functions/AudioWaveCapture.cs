using NAudio.Dsp;
using NAudio.Wave;

namespace DesktopEspDisplay.Functions;

public class AudioWaveCapture
{
    private const int NumBars = 32;
    private static readonly float[] SmoothedBars = new float[NumBars];
    private const int FftLength = 4096;
    private const float MinFreq = 20f;
    private const float MaxFreq = 20000f;
    private const float Smoothing = 0.5f;
    
    public WasapiLoopbackCapture Capture;
    
    public AudioWaveCapture()
    {
        Capture = new WasapiLoopbackCapture();
    }

    public byte[] GetAudio(WaveInEventArgs eventArgs)
    {
        var bytesPerSample = Capture.WaveFormat.BitsPerSample / 8;
        var samples = eventArgs.BytesRecorded / bytesPerSample;
        var audioBuffer = new float[samples];

        for (var i = 0; i < samples; i++)
        {
            audioBuffer[i] = bytesPerSample switch
            {
                4 => BitConverter.ToSingle(eventArgs.Buffer, i * 4),
                2 => BitConverter.ToInt16(eventArgs.Buffer, i * 2) / 32768f,
                _ => audioBuffer[i]
            };
        }

        // FFT with Hanning window
        var fftBuffer = new Complex[FftLength];
        for (var i = 0; i < FftLength && i < audioBuffer.Length; i++)
        {
            var window = 0.5f * (1 - (float)Math.Cos(2 * Math.PI * i / (FftLength - 1)));
            fftBuffer[i] = new Complex { X = audioBuffer[i] * window, Y = 0 };
        }

        FastFourierTransform.FFT(true, (int)Math.Log(FftLength, 2.0), fftBuffer);

        // magnitude spectrum
        var fftMagnitudes = new float[FftLength / 2];
        for (var i = 0; i < fftMagnitudes.Length; i++)
            fftMagnitudes[i] = (float)Math.Sqrt(fftBuffer[i].X * fftBuffer[i].X + fftBuffer[i].Y * fftBuffer[i].Y);

        float sampleRate = Capture.WaveFormat.SampleRate;
        var bars = new float[NumBars];

        // logarithmic mapping with averaging
        for (var i = 0; i < NumBars; i++)
        {
            var startFreq = MinFreq * Math.Pow(MaxFreq / MinFreq, (double)i / NumBars);
            var endFreq = MinFreq * Math.Pow(MaxFreq / MinFreq, (double)(i + 1) / NumBars);

            var startBin = startFreq / sampleRate * FftLength;
            var endBin = endFreq / sampleRate * FftLength;

            double sum = 0;
            var count = 0;

            for (var b = startBin; b < endBin; b += 0.5)
            {
                var idx = (int)b;
                if (idx >= fftMagnitudes.Length - 1) break;

                var frac = b - idx;
                var val = (float)((1 - frac) * fftMagnitudes[idx] + frac * fftMagnitudes[idx + 1]);
                sum += val;
                count++;
            }

            bars[i] = count > 0 ? (float)(sum / count) : 0;
        }

        // normalize and smooth
        var max = bars.Max();
        if (max > 0)
            for (var i = 0; i < NumBars; i++)
                bars[i] /= max;

        for (var i = 0; i < NumBars; i++)
            SmoothedBars[i] = Smoothing * SmoothedBars[i] + (1 - Smoothing) * bars[i];

        // send to ESP32
        var values = SmoothedBars.Select(f => (byte)(f * 255)).ToArray();
        return values;
    }
}