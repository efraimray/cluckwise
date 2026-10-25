using System.IO;
using System.Windows.Media;

namespace CluckWise;

/// <summary>Calm background sound for Deep Work, generated in code and looped quietly.</summary>
public static class Ambient
{
    public static readonly string[] Choices = { "None", "Brown noise", "Soft rain" };

    private const int Rate = 22050;
    private static MediaPlayer? _player;

    public static void Play(string choice)
    {
        Stop();
        if (choice is not ("Brown noise" or "Soft rain")) return;
        var folder = Path.Combine(Storage.Folder, "Sounds");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, choice.Replace(" ", "") + ".wav");
        if (!File.Exists(path)) Write(path, choice == "Soft rain" ? Rain() : Brown(30));

        _player = new MediaPlayer { Volume = 0.35 };
        _player.Open(new Uri(path));
        _player.MediaEnded += (_, _) => { if (_player != null) { _player.Position = TimeSpan.Zero; _player.Play(); } };
        _player.Play();
    }

    public static void Stop()
    {
        _player?.Stop();
        _player?.Close();
        _player = null;
    }

    private static float[] Brown(int seconds)
    {
        var rnd = new Random(7);
        var buf = new float[seconds * Rate];
        double last = 0;
        for (int i = 0; i < buf.Length; i++)
        {
            last = (last + 0.02 * (rnd.NextDouble() * 2 - 1)) / 1.02;
            buf[i] = (float)(last * 3.2);
        }
        Fade(buf);
        return buf;
    }

    private static float[] Rain()
    {
        var rnd = new Random(11);
        var buf = Brown(30);
        for (int i = 0; i < buf.Length; i++) buf[i] *= 0.6f;
        // light patter: many tiny, quiet droplets
        for (int k = 0; k < 2600; k++)
        {
            int start = rnd.Next(buf.Length - 2000);
            double f = 1800 + rnd.NextDouble() * 2400, amp = 0.02 + rnd.NextDouble() * 0.05;
            for (int i = 0; i < 1500; i++)
                buf[start + i] += (float)(amp * Math.Exp(-i / 140.0) * Math.Sin(2 * Math.PI * f * i / Rate));
        }
        Fade(buf);
        return buf;
    }

    // Short fade in/out so the loop restart doesn't click.
    private static void Fade(float[] buf)
    {
        int n = Rate / 4;
        for (int i = 0; i < n; i++)
        {
            float g = (float)i / n;
            buf[i] *= g;
            buf[buf.Length - 1 - i] *= g;
        }
    }

    private static void Write(string path, float[] samples)
    {
        using var w = new BinaryWriter(File.Create(path));
        int bytes = samples.Length * 2;
        w.Write("RIFF"u8.ToArray()); w.Write(36 + bytes); w.Write("WAVE"u8.ToArray());
        w.Write("fmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8.ToArray()); w.Write(bytes);
        foreach (var s in samples) w.Write((short)(Math.Clamp(s, -1f, 1f) * short.MaxValue));
    }
}
