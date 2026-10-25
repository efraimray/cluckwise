using System.IO;
using System.Windows.Media;
using System.Windows.Threading;

namespace CluckWise;

/// <summary>
/// CluckWise's own alarm sounds. They are synthesised in code (no audio files are shipped),
/// written once to %AppData%\CluckWise\Sounds, and played with WPF's MediaPlayer.
/// </summary>
public static class Sounds
{
    public const string RoosterCrow = "Rooster Crow";
    public const string SunriseChime = "Sunrise Chime";
    public const string GentleBeep = "Gentle Beep";
    public const string ClassicBell = "Classic Bell";
    public const string Marimba = "Marimba";

    public static readonly string[] BuiltIn = { RoosterCrow, SunriseChime, GentleBeep, ClassicBell, Marimba };

    private const int Rate = 22050;
    private static readonly string Folder = Path.Combine(Storage.Folder, "Sounds");

    private const double GentleStart = 0.35;
    private const double GentleSeconds = 20;

    private static MediaPlayer? _player;
    private static System.Media.SoundPlayer? _fallback;
    private static DispatcherTimer? _ramp;

    /// <summary>A built-in sound name, or the full path of the user's own file.</summary>
    public static string DisplayName(string sound) =>
        BuiltIn.Contains(sound) ? sound : Path.GetFileNameWithoutExtension(sound);

    public static void Play(string sound, int volume, bool gentle, bool loop)
    {
        Stop();
        var path = ResolvePath(sound);
        var target = Math.Clamp(volume, 0, 100) / 100.0;

        _player = new MediaPlayer();
        _player.MediaFailed += (_, _) => PlayFallback(path, loop);
        _player.Open(new Uri(path));
        if (loop) _player.MediaEnded += (_, _) => { _player.Position = TimeSpan.Zero; _player.Play(); };
        _player.Volume = gentle ? target * GentleStart : target;
        _player.Play();

        if (gentle)
        {
            var started = DateTime.Now;
            _ramp = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _ramp.Tick += (_, _) =>
            {
                var t = Math.Min(1.0, (DateTime.Now - started).TotalSeconds / GentleSeconds);
                if (_player != null) _player.Volume = target * (GentleStart + (1 - GentleStart) * t);
                if (t >= 1.0) _ramp?.Stop();
            };
            _ramp.Start();
        }
    }

    /// <summary>Plain WAV playback without volume control, used if MediaPlayer fails (e.g. Windows N without the Media Feature Pack).</summary>
    private static void PlayFallback(string path, bool loop)
    {
        try
        {
            if (!path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) path = ResolvePath(RoosterCrow);
            _fallback?.Stop();
            _fallback = new System.Media.SoundPlayer(path);
            if (loop) _fallback.PlayLooping(); else _fallback.Play();
        }
        catch { System.Media.SystemSounds.Exclamation.Play(); }
    }

    public static void Stop()
    {
        _fallback?.Stop();
        _fallback = null;
        _ramp?.Stop();
        _ramp = null;
        if (_player != null)
        {
            _player.Stop();
            _player.Close();
            _player = null;
        }
    }

    private static string ResolvePath(string sound)
    {
        if (!BuiltIn.Contains(sound))
        {
            if (File.Exists(sound)) return sound;
            sound = RoosterCrow; // the user's file was moved or deleted
        }

        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, sound.Replace(" ", "") + ".v2.wav");
        if (!File.Exists(path)) WriteWav(path, Synthesize(sound));
        return path;
    }

    private static float[] Synthesize(string sound) => sound switch
    {
        SunriseChime => Chime(new[] { 523.25, 659.25, 783.99, 1046.5 }, 0.32, 1.4),
        GentleBeep => Beeps(),
        ClassicBell => Bell(),
        Marimba => Chime(new[] { 392.0, 523.25, 659.25, 523.25, 783.99 }, 0.18, 0.5, marimba: true),
        _ => Crow(),
    };

    // A cartoon "cock-a-doodle-doo": four sung syllables with pitch glides and a little vibrato.
    private static float[] Crow()
    {
        var syllables = new (double dur, double f0, double f1, double f2)[]
        {
            (0.16, 560, 640, 620), (0.12, 600, 620, 600), (0.30, 640, 880, 780), (0.95, 820, 940, 560),
        };
        var output = new List<float>();
        foreach (var (dur, f0, f1, f2) in syllables)
        {
            int n = (int)(dur * Rate);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / n;
                double f = t < 0.5 ? Lerp(f0, f1, t * 2) : Lerp(f1, f2, (t - 0.5) * 2);
                f *= 1 + 0.012 * Math.Sin(2 * Math.PI * 7 * i / Rate);
                phase += 2 * Math.PI * f / Rate;
                double v = Math.Sin(phase) + 0.55 * Math.Sin(2 * phase) + 0.35 * Math.Sin(3 * phase)
                         + 0.2 * Math.Sin(4 * phase) + 0.1 * Math.Sin(5 * phase);
                double env = Math.Min(1, t * 12) * Math.Min(1, (1 - t) * 8);
                output.Add((float)(v * env * 0.28));
            }
            output.AddRange(new float[(int)(0.03 * Rate)]);
        }
        output.AddRange(new float[(int)(0.9 * Rate)]);
        return output.ToArray();
    }

    private static float[] Chime(double[] notes, double step, double tail, bool marimba = false)
    {
        int total = (int)((notes.Length * step + tail + 0.6) * Rate);
        var buf = new float[total];
        for (int k = 0; k < notes.Length; k++)
        {
            int start = (int)(k * step * Rate);
            double decay = marimba ? 9 : 3.2;
            for (int i = 0; start + i < total; i++)
            {
                double t = (double)i / Rate;
                double env = Math.Exp(-decay * t) * Math.Min(1, t * 400);
                double v = Math.Sin(2 * Math.PI * notes[k] * t) + (marimba ? 0.25 * Math.Sin(2 * Math.PI * notes[k] * 4 * t) : 0.3 * Math.Sin(2 * Math.PI * notes[k] * 2 * t));
                buf[start + i] += (float)(v * env * 0.3);
            }
        }
        return buf;
    }

    private static float[] Beeps()
    {
        var output = new List<float>();
        for (int b = 0; b < 3; b++)
        {
            int n = (int)(0.14 * Rate);
            for (int i = 0; i < n; i++)
            {
                double env = Math.Min(1, i / 200.0) * Math.Min(1, (n - i) / 200.0);
                output.Add((float)(Math.Sin(2 * Math.PI * 880 * i / Rate) * env * 0.35));
            }
            output.AddRange(new float[(int)(0.12 * Rate)]);
        }
        output.AddRange(new float[(int)(0.7 * Rate)]);
        return output.ToArray();
    }

    private static float[] Bell()
    {
        double[] partials = { 1.0, 2.0, 2.76, 5.4 };
        double[] amps = { 1.0, 0.5, 0.35, 0.15 };
        int n = (int)(1.8 * Rate);
        var buf = new float[n];
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / Rate;
            double v = 0;
            for (int p = 0; p < partials.Length; p++)
                v += amps[p] * Math.Exp(-(2.2 + p) * t) * Math.Sin(2 * Math.PI * 660 * partials[p] * t);
            buf[i] = (float)(v * 0.25 * Math.Min(1, t * 500));
        }
        return buf;
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static void WriteWav(string path, float[] samples)
    {
        // Normalise so every built-in sound peaks at about 90% (the originals were too quiet).
        float peak = samples.Length == 0 ? 0 : samples.Max(x => Math.Abs(x));
        if (peak > 0) for (int i = 0; i < samples.Length; i++) samples[i] = samples[i] / peak * 0.9f;

        using var w = new BinaryWriter(File.Create(path));
        int dataBytes = samples.Length * 2;
        w.Write("RIFF"u8.ToArray()); w.Write(36 + dataBytes); w.Write("WAVE"u8.ToArray());
        w.Write("fmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8.ToArray()); w.Write(dataBytes);
        foreach (var s in samples) w.Write((short)(Math.Clamp(s, -1f, 1f) * short.MaxValue));
    }
}
