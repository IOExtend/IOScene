namespace IOScene.Formats;

internal static class SceneFormatFile
{
    internal static void Replace(string path, string extension, Action<Stream> write)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        string temporaryPath = Path.Combine(Path.GetDirectoryName(fullPath)!, $".{Guid.NewGuid():N}.{extension}.tmp");
        var file = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        try
        {
            using (file) write(file);
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}
