using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CluckWise;

/// <summary>Saves and loads everything to %AppData%\CluckWise\data.json.</summary>
public static class Storage
{
    public static string Folder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CluckWise");

    private static string DataFile => Path.Combine(Folder, "data.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppData Load()
    {
        try
        {
            if (File.Exists(DataFile))
            {
                var data = JsonSerializer.Deserialize<AppData>(File.ReadAllText(DataFile), Options);
                if (data != null) return data;
            }
        }
        catch (Exception)
        {
            // A damaged file should not stop the app: keep a copy and start fresh.
            try { File.Copy(DataFile, DataFile + ".broken", overwrite: true); } catch { }
        }
        return CreateStarterData();
    }

    public static void Save(AppData data)
    {
        Directory.CreateDirectory(Folder);
        var tmp = DataFile + ".tmp";
        var toSave = data.ForSaving();
        File.WriteAllText(tmp, JsonSerializer.Serialize(toSave, Options));
        File.Move(tmp, DataFile, overwrite: true);
    }

    private static AppData CreateStarterData()
    {
        var data = new AppData();
        data.Alarms.Add(new Alarm { Hour = 6, Minute = 30, Label = "Rise & shine", Group = "Morning", Sound = Sounds.RoosterCrow });
        data.Alarms.Add(new Alarm
        {
            Hour = 9, Minute = 0, Label = "Start my workday", Group = "Work", Sound = Sounds.GentleBeep,
            Enabled = false,
            Actions = { new AlarmAction { Kind = ActionKind.OpenWebsite, Target = "https://www.timeanddate.com/weather/" } },
        });
        foreach (var t in new[] { "Drink a glass of water", "Stretch for 2 minutes", "Plan my top 3 tasks", "Make the bed" })
            data.Tasks.Add(new MorningTask { Text = t });
        return data;
    }
}
