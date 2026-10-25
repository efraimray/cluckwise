using System.Diagnostics;
using System.IO;

namespace CluckWise;

/// <summary>Runs an alarm's "When it rings…" steps.</summary>
public static class ActionRunner
{
    public static void RunAll(Alarm alarm)
    {
        foreach (var action in alarm.Actions)
        {
            var error = Run(action);
            if (error != null) App.Current.ShowTrayMessage("Couldn't run an action", error);
        }
    }

    /// <summary>Runs one step. Returns null on success, or a short reason it failed.</summary>
    public static string? Run(AlarmAction action)
    {
        try
        {
            switch (action.Kind)
            {
                case ActionKind.OpenWebsite:
                    var url = NormalizeUrl(action.Target);
                    if (url == null) return $"\"{action.Target}\" isn't a web address.";
                    Open(url);
                    return null;

                case ActionKind.LockPC:
                    return NativeMethods.LockWorkStation() ? null : "Windows didn't allow locking the PC.";

                case ActionKind.ScreenOff:
                    // Give the ringing popup a moment to appear before the screen goes dark.
                    Delay(TimeSpan.FromSeconds(2), NativeMethods.MonitorOff);
                    return null;

                case ActionKind.Sleep:
                    new SleepCountdownWindow().Show();
                    return null;

                case ActionKind.PlayMusic:
                case ActionKind.OpenProgram:
                    var path = Environment.ExpandEnvironmentVariables(action.Target.Trim().Trim('"'));
                    if (!File.Exists(path) && !Directory.Exists(path)) return $"\"{action.Target}\" wasn't found.";
                    Open(path);
                    return null;
            }
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
        return null;
    }

    /// <summary>Accepts "example.com" or a full http(s) address; anything else is rejected.</summary>
    public static string? NormalizeUrl(string text)
    {
        text = text.Trim();
        if (!text.Contains("://")) text = "https://" + text;
        return Uri.TryCreate(text, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
               && uri.Host.Contains('.')
            ? uri.ToString()
            : null;
    }

    private static void Delay(TimeSpan wait, Action then)
    {
        var t = new System.Windows.Threading.DispatcherTimer { Interval = wait };
        t.Tick += (_, _) => { t.Stop(); then(); };
        t.Start();
    }

    private static void Open(string target) =>
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
}
