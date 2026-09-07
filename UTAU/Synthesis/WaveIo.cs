using NAudio.Wave;
using System.IO;

namespace UTAU.Synthesis;

internal static class WaveIo
{
    const int ReadBlockSamples = 16384;

    public static AudioSample Read(string path)
    {
        using var reader = new AudioFileReader(path);
        var channels = Math.Max(reader.WaveFormat.Channels, 1);
        var buffer = new float[ReadBlockSamples * channels];
        var frames = new List<double>(Math.Max((int)(reader.Length / Math.Max(reader.WaveFormat.BlockAlign, 1)), 0));

        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (var i = 0; i + channels <= read; i += channels)
            {
                var sum = 0.0;
                for (var c = 0; c < channels; c++)
                    sum += buffer[i + c];
                frames.Add(sum / channels);
            }
        }

        return new AudioSample([.. frames], reader.WaveFormat.SampleRate);
    }

    public static void Write(string path, ReadOnlySpan<double> samples, int sampleRate)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using var writer = new WaveFileWriter(path, new WaveFormat(sampleRate, 16, 1));
        var buffer = new byte[samples.Length * sizeof(short)];
        for (var i = 0; i < samples.Length; i++)
        {
            var value = (int)Math.Round(Math.Clamp(samples[i], -1.0, 1.0) * short.MaxValue, MidpointRounding.AwayFromZero);
            var sample = (short)Math.Clamp(value, short.MinValue, short.MaxValue);
            buffer[i * 2] = (byte)(sample & 0xFF);
            buffer[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }
        writer.Write(buffer, 0, buffer.Length);
    }
}
