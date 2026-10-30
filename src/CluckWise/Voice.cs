using System.Speech.Synthesis;

namespace CluckWise;

/// <summary>Speaks short messages with Windows' built-in voice. The alarm sound is turned down while it talks.</summary>
public static class Voice
{
    private static SpeechSynthesizer? _synth;
    private static int _speaking;

    public static void Say(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        try
        {
            if (_synth == null)
            {
                _synth = new SpeechSynthesizer();
                _synth.SetOutputToDefaultAudioDevice();
                _synth.Rate = -1; // a touch slower, easier to follow when half asleep
                _synth.SpeakCompleted += (_, _) =>
                {
                    if (--_speaking <= 0) { _speaking = 0; App.Current.Dispatcher.Invoke(() => Sounds.Duck(false)); }
                };
            }
            _speaking++;
            Sounds.Duck(true);
            _synth.SpeakAsync(text);
        }
        catch
        {
            // No voice installed or speech unavailable: the alarm still rings normally.
            _speaking = 0;
            Sounds.Duck(false);
        }
    }

    public static void Stop()
    {
        try { _synth?.SpeakAsyncCancelAll(); } catch { }
        _speaking = 0;
        Sounds.Duck(false);
    }
}
