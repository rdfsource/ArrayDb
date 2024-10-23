using System.Text;

namespace Aiursoft.ArrayDb;

/// <summary>
/// StringRepository is a class designed to handle the storage and retrieval of string data within a specified file.
/// It manages the strings' offsets in the file and provides methods to save new strings and retrieve existing ones.
/// </summary>
public class StringRepository
{
    private readonly FileAccessService _fileAccess;
    public long FileEndOffset;
    private readonly object _expandSizeLock = new();
    private const int EndOffsetSize = sizeof(long); // We reserve the first 8 bytes for EndOffset

    /// <summary>
    /// StringRepository is a class designed to handle the storage and retrieval of string data within a specified file.
    /// It manages the strings' offsets in the file and provides methods to save new strings and retrieve existing ones.
    /// </summary>
    public StringRepository(string stringFilePath, long initialSizeIfNotExists)
    {
        _fileAccess = new(stringFilePath, initialSizeIfNotExists);
        FileEndOffset = GetStringFileEndOffset();
    }

    private long GetStringFileEndOffset()
    {
        var buffer = _fileAccess.ReadInFile(0, EndOffsetSize);
        var offSet = BitConverter.ToInt64(buffer, 0);
        // When initially the file is empty, we need to reserve the first 8 bytes for EndOffset
        return offSet <= EndOffsetSize ? EndOffsetSize : offSet;
    }

    public (long offset, int stringLength) WriteStringContentAndGetOffset(string? str)
    {
        switch (str)
        {
            case "":
                return (-1, 0); // -1 offset indicates empty string
            case null:
                return (-2, 0); // -2 offset indicates null string
        }

        var stringBytes = Encoding.UTF8.GetBytes(str);

        long currentOffset;
        lock (_expandSizeLock) // Lock to prevent multiple threads from expanding the file size at the same time
        {
            currentOffset = FileEndOffset;
            var newOffset = currentOffset + stringBytes.Length;
            FileEndOffset = newOffset;
        }
        
        // Save the string content to the string file
        _fileAccess.WriteInFile(currentOffset, stringBytes);
        
        // Update the end offset in the string file
        // TODO: This should be done asynchronously to avoid blocking the main thread.
        _fileAccess.WriteInFile(0, BitConverter.GetBytes(FileEndOffset));

        return (currentOffset, stringBytes.Length);
    }

    public string? LoadStringContent(long offset, int length)
    {
        switch (offset)
        {
            case -1:
                return string.Empty;
            case -2:
                return null;
            default:
            {
                var stringBytes = _fileAccess.ReadInFile(offset, length);
                return Encoding.UTF8.GetString(stringBytes);
            }
        }
    }
}