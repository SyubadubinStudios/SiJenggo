using System.Text.Json;

namespace SiJenggo.Game.Persistence;

/// <summary>
/// Loads and saves the profile. Writes go to a temp file first and are then swapped in,
/// so a crash mid-save never leaves a truncated profile; the previous file is kept as a backup.
/// </summary>
public sealed class SaveService
{
    private readonly string _path;

    public SaveService(string? directory = null)
    {
        directory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SiJenggo");
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "profile.json");
    }

    public string ProfilePath => _path;

    public Profile Load()
    {
        foreach (string candidate in new[] { _path, _path + ".bak" })
        {
            try
            {
                if (File.Exists(candidate))
                {
                    using var stream = File.OpenRead(candidate);
                    var profile = JsonSerializer.Deserialize(stream, ProfileJsonContext.Default.Profile);
                    if (profile is not null)
                    {
                        return profile;
                    }
                }
            }
            catch (JsonException)
            {
                // Corrupt file: fall through to the backup, then to a fresh profile.
            }
            catch (IOException)
            {
            }
        }

        return new Profile();
    }

    public void Save(Profile profile)
    {
        string temp = _path + ".tmp";
        using (var stream = File.Create(temp))
        {
            JsonSerializer.Serialize(stream, profile, ProfileJsonContext.Default.Profile);
        }

        if (File.Exists(_path))
        {
            File.Replace(temp, _path, _path + ".bak", ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(temp, _path);
        }
    }

    public static string Serialize(Profile profile) => JsonSerializer.Serialize(profile, ProfileJsonContext.Default.Profile);

    public static Profile? Deserialize(string json) => JsonSerializer.Deserialize(json, ProfileJsonContext.Default.Profile);
}
