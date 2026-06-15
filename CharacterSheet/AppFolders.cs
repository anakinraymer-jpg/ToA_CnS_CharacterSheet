using System.IO;

namespace CharacterSheet;

internal static class AppFolders
{
    private static readonly string Base = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "ToA CnS");

    public static readonly string Sheets = EnsureCreated(Path.Combine(Base, "Sheets"));
    public static readonly string Maps   = EnsureCreated(Path.Combine(Base, "Maps"));

    private static string EnsureCreated(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
