namespace Kahraman.Core;

/// <summary>Dateinamen im Datenordner: "taetigkeiten.kahraman" und "benutzername.kahraman".</summary>
public static class DataFiles
{
    public const string Extension = ".kahraman";
    public const string StoreBaseName = "taetigkeiten";

    public static string StorePath(string dir) => Path.Combine(dir, StoreBaseName + Extension);
    public static string LogPath(string dir, string user) => Path.Combine(dir, user + Extension);
}
