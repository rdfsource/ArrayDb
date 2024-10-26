using System.Diagnostics.CodeAnalysis;
using System.Text;
using Aiursoft.ArrayDb.Engine.Models;
using Aiursoft.ArrayDb.FilePersists;
using Aiursoft.ArrayDb.FilePersists.Services;

namespace Aiursoft.ArrayDb.Engine.ObjectStorage;

/// <summary>
/// StringRepository is a class designed to handle the storage and retrieval of string data within a specified file.
/// It manages the strings' offsets in the file and provides methods to save new strings and retrieve existing ones.
/// </summary>
public class StringRepository
{
    // Save the offset.
    public long FileEndOffset;
    private const int EndOffsetSize = sizeof(long);
    private readonly object _expandSizeLock = new();
    
    // Underlying storage
    private readonly CachedFileAccessService _fileAccess;
    
    // Statistics
    public int RequestWriteSpaceCount;
    public int LoadStringContentCount;
    public int BulkWriteStringsCount;
    
    [ExcludeFromCodeCoverage]
    public void ResetAllStatistics()
    {
        RequestWriteSpaceCount = 0;
        LoadStringContentCount = 0;
        BulkWriteStringsCount = 0;
    }
    
    public string OutputStatistics()
    {
        return $@"
String repository statistics:

* Logical file end offset: {FileEndOffset}
* Request write space events count: {RequestWriteSpaceCount}
* Load string content events count: {LoadStringContentCount}
* Bulk write strings events count: {BulkWriteStringsCount}

Underlying cached file access service statistics:
{_fileAccess.OutputCacheReport().AppendTabsEachLineHead()}
";
    }

    /// <summary>
    /// StringRepository is a class designed to handle the storage and retrieval of string data within a specified file.
    /// It manages the strings' offsets in the file and provides methods to save new strings and retrieve existing ones.
    /// </summary>
    public StringRepository(
        string stringFilePath, 
        long initialUnderlyingFileSizeIfNotExists, 
        int cachePageSize, 
        int maxCachedPagesCount,
        int hotCacheItems)
    {
        _fileAccess = new(
            path: stringFilePath,
            initialUnderlyingFileSizeIfNotExists: initialUnderlyingFileSizeIfNotExists,
            cachePageSize: cachePageSize,
            maxCachedPagesCount: maxCachedPagesCount,
            hotCacheItems: hotCacheItems);
        FileEndOffset = GetStringFileEndOffset();
    }

    private long GetStringFileEndOffset()
    {
        var buffer = _fileAccess.ReadInFile(0, EndOffsetSize);
        var offSet = BitConverter.ToInt64(buffer, 0);
        // When initially the file is empty, we need to reserve the first 8 bytes for EndOffset
        return offSet <= EndOffsetSize ? EndOffsetSize : offSet;
    }

    private long RequestWriteSpaceAndGetStartOffset(int length)
    {
        long writeOffset;
        lock (_expandSizeLock)
        {
            writeOffset = FileEndOffset;
            FileEndOffset += length;
            _fileAccess.WriteInFile(0, BitConverter.GetBytes(FileEndOffset));
        }
        Interlocked.Increment(ref RequestWriteSpaceCount);
        return writeOffset;
    }
    
    public SavedString[] BulkWriteStringContentAndGetOffsets(byte[][] processedStrings)
    {
        var allBytes = processedStrings.SelectMany(x => x).ToArray();
        var writeOffset = RequestWriteSpaceAndGetStartOffset(allBytes.Length);
        _fileAccess.WriteInFile(writeOffset, allBytes);
        var offset = writeOffset;
        var result = new SavedString[processedStrings.Length];
        var index = 0;
        foreach (var processedString in processedStrings)
        {
            result[index] = new SavedString { Offset = offset, Length = processedString.Length };
            offset += processedString.Length;
            index++;
        }

        Interlocked.Increment(ref BulkWriteStringsCount);
        return result;
    }

    public string? LoadStringContent(long offset, int length)
    {
        Interlocked.Increment(ref LoadStringContentCount);
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