using Microsoft.Build.Framework;
using Microsoft.Web.XmlTransform;

namespace Rehost.Web.Build.Tasks;

public sealed class ApplyXdtTransform : Microsoft.Build.Utilities.Task
{
    [Required]
    public string Source { get; set; } = string.Empty;

    [Required]
    public string Transform { get; set; } = string.Empty;

    [Required]
    public string Destination { get; set; } = string.Empty;

    public override bool Execute()
    {
        if (!File.Exists(Source))
        {
            Log.LogError($"XDT source '{Source}' does not exist.");
            return false;
        }

        if (!File.Exists(Transform))
        {
            Log.LogError($"XDT transform '{Transform}' does not exist.");
            return false;
        }

        using var document = new XmlTransformableDocument();
        document.PreserveWhitespace = true;
        document.Load(Source);

        using var transformation = new XmlTransformation(Transform, new MSBuildTransformationLogger(Log));
        if (!transformation.Apply(document))
        {
            Log.LogError($"XDT transform '{Transform}' failed for '{Source}'.");
            return false;
        }

        var destinationDirectory = Path.GetDirectoryName(Path.GetFullPath(Destination));
        if (destinationDirectory is not null)
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        document.Save(Destination);
        return true;
    }

    private sealed class MSBuildTransformationLogger(Microsoft.Build.Utilities.TaskLoggingHelper log) : IXmlTransformationLogger
    {
        public void LogMessage(string message, params object[] messageArgs)
            => log.LogMessage(MessageImportance.Low, message, messageArgs);

        public void LogMessage(MessageType type, string message, params object[] messageArgs)
            => log.LogMessage(MessageImportance.Low, message, messageArgs);

        public void LogWarning(string message, params object[] messageArgs)
            => log.LogWarning(message, messageArgs);

        public void LogWarning(string file, string message, params object[] messageArgs)
            => log.LogWarning(null, null, null, file, 0, 0, 0, 0, message, messageArgs);

        public void LogWarning(string file, int lineNumber, int linePosition, string message, params object[] messageArgs)
            => log.LogWarning(null, null, null, file, lineNumber, linePosition, 0, 0, message, messageArgs);

        public void LogError(string message, params object[] messageArgs)
            => log.LogError(message, messageArgs);

        public void LogError(string file, string message, params object[] messageArgs)
            => log.LogError(null, null, null, file, 0, 0, 0, 0, message, messageArgs);

        public void LogError(string file, int lineNumber, int linePosition, string message, params object[] messageArgs)
            => log.LogError(null, null, null, file, lineNumber, linePosition, 0, 0, message, messageArgs);

        public void LogErrorFromException(Exception ex)
            => log.LogErrorFromException(ex);

        public void LogErrorFromException(Exception ex, string file)
            => log.LogErrorFromException(ex, showStackTrace: false, showDetail: false, file);

        public void LogErrorFromException(Exception ex, string file, int lineNumber, int linePosition)
            => log.LogError(null, null, null, file, lineNumber, linePosition, 0, 0, ex.Message);

        public void StartSection(string message, params object[] messageArgs)
            => log.LogMessage(MessageImportance.Low, message, messageArgs);

        public void StartSection(MessageType type, string message, params object[] messageArgs)
            => log.LogMessage(MessageImportance.Low, message, messageArgs);

        public void EndSection(string message, params object[] messageArgs)
            => log.LogMessage(MessageImportance.Low, message, messageArgs);

        public void EndSection(MessageType type, string message, params object[] messageArgs)
            => log.LogMessage(MessageImportance.Low, message, messageArgs);
    }
}
