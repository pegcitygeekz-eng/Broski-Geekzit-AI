using System.IO;
using System.Text.Json;

namespace ChoppaAI;

internal sealed class OrganizerDataStore
{
    private static readonly string DataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BroskiAI",
        "organizer.json");

    public List<AlarmRecord> Alarms { get; set; } = [];
    public List<AppointmentRecord> Appointments { get; set; } = [];
    public List<DeviceRecord> Devices { get; set; } = [];

    public static OrganizerDataStore Load()
    {
        if (!File.Exists(DataPath))
        {
            return new OrganizerDataStore();
        }

        var organizer = JsonSerializer.Deserialize<OrganizerDataStore>(File.ReadAllText(DataPath))
            ?? throw new InvalidDataException("The saved organizer file is empty or invalid.");
        if (organizer.Alarms is null || organizer.Appointments is null || organizer.Devices is null)
        {
            throw new InvalidDataException("The saved organizer file is missing one or more record lists.");
        }

        return organizer;
    }

    public void Save()
    {
        var directory = Path.GetDirectoryName(DataPath)
            ?? throw new InvalidOperationException("The organizer data directory could not be determined.");
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{DataPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true,
            }));
            File.Move(temporaryPath, DataPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}

internal sealed record AlarmRecord(Guid Id, string Title, DateTimeOffset DueAt);

internal sealed record AppointmentRecord(
    Guid Id,
    string Title,
    DateTimeOffset StartsAt,
    string Location,
    string Notes);

internal sealed record DeviceRecord(
    Guid Id,
    string Device,
    string Issue,
    string Status,
    string Notes,
    DateTimeOffset LoggedAt);
