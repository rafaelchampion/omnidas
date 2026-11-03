namespace Omnidas.Core.Models;

public enum MediaCategory
{
    Audio,
    Video,
    Image,
    Document,
    Archive,
    Other
}

public record FormatDefinition(
    string Extension,
    string Name,
    MediaCategory Category,
    string MimeType,
    string[] AlternativeExtensions,
    bool CanRead,
    bool CanWrite,
    string Description
);
