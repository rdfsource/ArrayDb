using System.Diagnostics;
using Aiursoft.ArrayDb.ObjectBucket;
using Aiursoft.ArrayDb.Tests.Base;
using Aiursoft.ArrayDb.Tests.Base.Models;
using Aiursoft.ArrayDb.WriteBuffer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aiursoft.ArrayDb.Tests.DatabaseEngineTests.PerformanceTests;

[TestClass]
[DoNotParallelize]
public class PerformanceTestBuffered : ArrayDbTestBase
{
    [TestMethod]
    public async Task PerformanceTestParallelBufferedWrite()
    {
        var bucket =
            new ObjectBucket<SampleData>(TestFilePath, TestFilePathStrings);
        var buffer = new BufferedObjectBuckets<SampleData>(bucket);
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        Parallel.For(0, 1000000, i =>
        {
            var sample = new SampleData
            {
                MyNumber1 = i,
                MyString1 = $"Hello, World! 你好世界 {i}",
                MyNumber2 = i * 10,
                MyBoolean1 = i % 2 == 0,
                MyString2 = $"This is another longer string. {i}"
            };
            buffer.AddBuffered(sample);
        });
        stopWatch.Stop();
        Console.WriteLine(buffer.OutputStatistics());
        Console.WriteLine($"Write 1000000 times: {stopWatch.ElapsedMilliseconds}ms");
        // Read 100 0000 times in less than 10 seconds. On my machine 345ms.
        Assert.IsTrue(stopWatch.Elapsed.TotalSeconds < 10);
        
        Assert.IsTrue(buffer.IsHot);
        Assert.IsTrue(buffer.BufferedItemsCount > 1);
        Assert.IsTrue(bucket.SpaceProvisionedItemsCount < 1000000);
        
        stopWatch.Reset();
        stopWatch.Start();
        await buffer.SyncAsync();
        stopWatch.Stop();

        Console.WriteLine($"Sync buffer: {stopWatch.ElapsedMilliseconds}ms");
        // Sync 1000 0000 times in less than 10 seconds. On my machine 119ms.
        Assert.IsTrue(stopWatch.Elapsed.TotalSeconds < 10);
        Assert.AreEqual(0, buffer.BufferedItemsCount);
        Assert.AreEqual(1000000, bucket.SpaceProvisionedItemsCount);
        Console.WriteLine(buffer.OutputStatistics());
    }
    
    [TestMethod]
    public async Task PerformanceTestSequentialBufferedWrite()
    {
        var bucket =
            new ObjectBucket<SampleData>(TestFilePath, TestFilePathStrings);
        var buffer = new BufferedObjectBuckets<SampleData>(bucket, initialCooldownMilliseconds: 50);
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        for (var i = 0; i < 1000000; i++)
        {
            var sample = new SampleData
            {
                MyNumber1 = i,
                MyString1 = $"Hello, World! 你好世界 {i}",
                MyNumber2 = i * 10,
                MyBoolean1 = i % 2 == 0,
                MyString2 = $"This is another longer string. {i}"
            };
            buffer.AddBuffered(sample);
        }
        stopWatch.Stop();
        Console.WriteLine(buffer.OutputStatistics());
        Console.WriteLine($"Write 1000000 times: {stopWatch.ElapsedMilliseconds}ms");
        // Read 100 0000 times in less than 10 seconds. On my machine 597ms.
        Assert.IsTrue(stopWatch.Elapsed.TotalSeconds < 50);
        
        Assert.IsTrue(buffer.IsHot);
        Assert.IsTrue(buffer.BufferedItemsCount > 1);
        Assert.IsTrue(bucket.SpaceProvisionedItemsCount < 1000000);
        
        stopWatch.Reset();
        stopWatch.Start();
        await buffer.SyncAsync();
        stopWatch.Stop();

        Console.WriteLine($"Sync buffer: {stopWatch.ElapsedMilliseconds}ms");
        // Sync 1000 0000 times in less than 10 seconds. On my machine 119ms.
        Assert.IsTrue(stopWatch.Elapsed.TotalSeconds < 100);
        Assert.AreEqual(0, buffer.BufferedItemsCount);
        Assert.AreEqual(1000000, bucket.SpaceProvisionedItemsCount);
        Console.WriteLine(buffer.OutputStatistics());
    }
}