using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace CluckWise;

/// <summary>Switches every CluckWise colour between the light and dark palettes, live.</summary>
public static class Theme
{
    public static bool IsDark { get; private set; }

    private static readonly Dictionary<string, (string Light, string Dark)> Palette = new()
    {
        ["Ground"] = ("#F7F5F0", "#171411"),
        ["Surface"] = ("#FFFFFF", "#221E1A"),
        ["SurfaceAlt"] = ("#FBFAF7", "#1D1A16"),
        ["Line"] = ("#ECE6DC", "#342E28"),
        ["FieldLine"] = ("#E4DDD2", "#3D362F"),
        ["Chip"] = ("#F3EFE8", "#2E2823"),
        ["Muted"] = ("#6B6259", "#ADA397"),
        ["Text"] = ("#1F1B16", "#F2EDE6"),
        ["Ink"] = ("#1F1B16", "#0E0C0A"),
        ["InkSoft"] = ("#2C2620", "#26211C"),
        ["YolkSoft"] = ("#FFF1D6", "#3B2F18"),
        ["SwitchOff"] = ("#D9D2C7", "#4A433B"),
        ["SideText"] = ("#D9D0C4", "#D9D0C4"),
        ["Yolk"] = ("#F5A524", "#F5A524"),
    };

    public static void Apply(bool dark)
    {
        IsDark = dark;
        var res = Application.Current.Resources;
        foreach (var (key, (light, darkHex)) in Palette)
            res[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? darkHex : light));
        foreach (Window w in Application.Current.Windows) ApplyTitleBar(w);
    }

    // Windows 10 (20H1+) and 11: dark title bar to match.
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void ApplyTitleBar(Window w)
    {
        var hwnd = new WindowInteropHelper(w).Handle;
        if (hwnd == IntPtr.Zero) return;
        int on = IsDark ? 1 : 0;
        try { DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int)); } catch { }
    }
}
