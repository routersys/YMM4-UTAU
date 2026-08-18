using System.IO;
using System.Text;
using UTAU.Models;
using UTAU.Synthesis;

namespace UTAU.Tests;

internal static class TestVoiceBank
{
    public const int SampleRate = 44100;
    public const double SampleDurationMilliseconds = 1000.0;
    public const double SourceFrequency = 200.0;

    public static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "UTAU.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public static double[] CreateVowel(double frequency = SourceFrequency, double durationMilliseconds = SampleDurationMilliseconds)
    {
        var length = (int)(durationMilliseconds * SampleRate / 1000.0);
        var samples = new double[length];
        double[] formants = [700.0, 1200.0, 2600.0];
        double[] gains = [1.0, 0.5, 0.2];

        for (var i = 0; i < length; i++)
        {
            var time = i / (double)SampleRate;
            var value = 0.0;
            for (var harmonic = 1; harmonic * frequency < SampleRate / 2.0; harmonic++)
            {
                var harmonicFrequency = harmonic * frequency;
                var amplitude = 0.0;
                for (var f = 0; f < formants.Length; f++)
                {
                    var distance = (harmonicFrequency - formants[f]) / 260.0;
                    amplitude += gains[f] * Math.Exp(-distance * distance);
                }
                value += amplitude * Math.Sin(2.0 * Math.PI * harmonicFrequency * time) / harmonic;
            }
            samples[i] = 0.2 * value;
        }

        return samples;
    }

    public static void WriteSample(string directory, string fileName, double[]? samples = null)
        => WaveIo.Write(Path.Combine(directory, fileName), samples ?? CreateVowel(), SampleRate);

    public static void WriteText(string directory, string fileName, string content, Encoding? encoding = null)
        => File.WriteAllBytes(Path.Combine(directory, fileName), (encoding ?? VoiceBankTextReader.ShiftJis).GetBytes(content));

    public static VoiceBank CreateSingleKanaBank(string directory)
    {
        WriteText(directory, VoiceBankLoader.CharacterFileName, "name=試験音源\r\nauthor=試験\r\n");
        WriteText(
            directory,
            VoiceBankLoader.OtoFileName,
            string.Join("\r\n",
            [
                "a.wav=あ,50,80,-500,100,40",
                "ka.wav=か,50,120,-500,140,50",
                "sa.wav=さ,50,120,-500,140,50",
                "n.wav=ん,50,60,-400,80,30",
            ]));
        WriteSample(directory, "a.wav");
        WriteSample(directory, "ka.wav");
        WriteSample(directory, "sa.wav");
        WriteSample(directory, "n.wav");
        return VoiceBankLoader.Load("test", directory);
    }

    public static VoiceBank CreateVcvBank(string directory)
    {
        WriteText(directory, VoiceBankLoader.CharacterFileName, "name=連続音\r\n");
        WriteText(
            directory,
            VoiceBankLoader.OtoFileName,
            string.Join("\r\n",
            [
                "start.wav=- あ,50,80,-500,100,40",
                "aka.wav=a か,50,120,-500,140,50",
                "aa.wav=a あ,50,80,-500,100,40",
            ]));
        WriteSample(directory, "start.wav");
        WriteSample(directory, "aka.wav");
        WriteSample(directory, "aa.wav");
        return VoiceBankLoader.Load("vcv", directory);
    }

    public static VoiceBank CreateCvvcBank(string directory)
    {
        WriteText(directory, VoiceBankLoader.CharacterFileName, "name=CVVC\r\n");
        WriteText(
            directory,
            VoiceBankLoader.OtoFileName,
            string.Join("\r\n",
            [
                "a.wav=あ,50,80,-500,100,40",
                "ka.wav=か,50,120,-500,140,50",
                "ak.wav=a k,50,40,-200,60,20",
                "aend.wav=a -,50,40,-200,60,20",
            ]));
        WriteSample(directory, "a.wav");
        WriteSample(directory, "ka.wav");
        WriteSample(directory, "ak.wav");
        WriteSample(directory, "aend.wav");
        return VoiceBankLoader.Load("cvvc", directory);
    }
}
