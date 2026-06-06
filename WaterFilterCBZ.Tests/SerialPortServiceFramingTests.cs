using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using WaterFilterCBZ.Models;
using WaterFilterCBZ.Services;

namespace WaterFilterCBZ.Tests;

/// <summary>
/// Exercises the binary frame parser in <see cref="SerialPortService"/> without any real
/// serial hardware: bytes are pushed straight into the private receive buffer / queue and the
/// parsing methods are invoked via reflection (the same approach as the existing ParseFrame test).
/// Frame layout: START(0xAA) | count | count*[id,ts(4),unit,float(4)] | checksum | END(0x55).
/// </summary>
public class SerialPortServiceFramingTests
{
    // ----- framing helpers -------------------------------------------------

    /// <summary>Builds a structurally valid frame with a correct trailing checksum.</summary>
    private static byte[] BuildFrame(params (byte sensorId, uint timestampMs, byte unitId, float value)[] entries)
    {
        var body = new List<byte> { 0xAA, (byte)entries.Length };
        foreach (var e in entries)
        {
            body.Add(e.sensorId);
            body.AddRange(BitConverter.GetBytes(e.timestampMs));
            body.Add(e.unitId);
            body.AddRange(BitConverter.GetBytes(e.value));
        }

        // Checksum = (sum of every byte from START through the last payload byte) & 0xFF.
        int sum = 0;
        foreach (var b in body)
            sum = (sum + b) & 0xFF;

        body.Add((byte)sum); // checksum
        body.Add(0x55);      // END
        return body.ToArray();
    }

    private static byte[] SimpleFrame(byte sensorId = 0x01, float value = 3.5f)
        => BuildFrame((sensorId, 1000u, 0x01, value));

    // ----- reflection plumbing --------------------------------------------

    private static List<byte> ReceiveBuffer(SerialPortService service)
        => (List<byte>)typeof(SerialPortService)
            .GetField("_receiveBuffer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(service)!;

    private static void SetSawStartByteAt(SerialPortService service, DateTime? value)
        => typeof(SerialPortService)
            .GetField("_sawStartByteAtUtc", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(service, value);

    private static void InvokeParseReceiveBuffer(SerialPortService service)
        => typeof(SerialPortService)
            .GetMethod("ParseReceiveBuffer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(service, null);

    private static bool InvokeValidateChecksum(byte[] frame)
        => (bool)typeof(SerialPortService)
            .GetMethod("ValidateChecksum", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { frame })!;

    private static void Enqueue(SerialPortService service, byte[] chunk)
    {
        var queue = (ConcurrentQueue<byte[]>)typeof(SerialPortService)
            .GetField("_byteQueue", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(service)!;
        queue.Enqueue(chunk);
    }

    private static Task InvokeProcessIncomingDataAsync(SerialPortService service, CancellationToken ct)
        => (Task)typeof(SerialPortService)
            .GetMethod("ProcessIncomingDataAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(service, new object[] { ct })!;

    private static (SerialPortService service, List<SensorSample> samples) NewService()
    {
        var samples = new List<SensorSample>();
        var service = new SerialPortService("COM1", s => { lock (samples) { samples.Add(s); } });
        return (service, samples);
    }

    // ----- ValidateChecksum -----------------------------------------------

    [Fact]
    public void ValidateChecksum_AcceptsWellFormedFrame()
    {
        Assert.True(InvokeValidateChecksum(SimpleFrame()));
    }

    [Fact]
    public void ValidateChecksum_RejectsTooShortFrame()
    {
        Assert.False(InvokeValidateChecksum(new byte[] { 0xAA, 0x55, 0x00 }));
    }

    [Fact]
    public void ValidateChecksum_RejectsWrongStartOrEndByte()
    {
        var frame = SimpleFrame();

        var badStart = (byte[])frame.Clone();
        badStart[0] = 0x00;
        Assert.False(InvokeValidateChecksum(badStart));

        var badEnd = (byte[])frame.Clone();
        badEnd[^1] = 0x00;
        Assert.False(InvokeValidateChecksum(badEnd));
    }

    [Fact]
    public void ValidateChecksum_RejectsCorruptedChecksum()
    {
        var frame = SimpleFrame();
        frame[^2] ^= 0xFF; // flip the checksum byte
        Assert.False(InvokeValidateChecksum(frame));
    }

    // ----- ParseReceiveBuffer ---------------------------------------------

    [Fact]
    public void ParseReceiveBuffer_ParsesSingleValidFrame_AndConsumesBuffer()
    {
        var (service, samples) = NewService();
        using var _ = service;
        ReceiveBuffer(service).AddRange(SimpleFrame(0x02, 12.5f));

        InvokeParseReceiveBuffer(service);

        var sample = Assert.Single(samples);
        Assert.Equal("0x02", sample.SensorId);
        Assert.Equal(12.5f, sample.Value);
        Assert.Empty(ReceiveBuffer(service));
    }

    [Fact]
    public void ParseReceiveBuffer_SkipsGarbageBeforeStartByte()
    {
        var (service, samples) = NewService();
        using var _ = service;
        ReceiveBuffer(service).AddRange(new byte[] { 0x11, 0x22, 0x33 });
        ReceiveBuffer(service).AddRange(SimpleFrame());

        InvokeParseReceiveBuffer(service);

        Assert.Single(samples);
        Assert.Empty(ReceiveBuffer(service));
    }

    [Fact]
    public void ParseReceiveBuffer_NoStartByte_ClearsBuffer()
    {
        var (service, samples) = NewService();
        using var _ = service;
        ReceiveBuffer(service).AddRange(new byte[] { 0x01, 0x02, 0x03 });

        InvokeParseReceiveBuffer(service);

        Assert.Empty(samples);
        Assert.Empty(ReceiveBuffer(service));
    }

    [Fact]
    public void ParseReceiveBuffer_PartialFrame_WaitsWithoutEmittingSample()
    {
        var (service, samples) = NewService();
        using var _ = service;
        var partial = SimpleFrame()[..6]; // start + count + a few payload bytes only
        ReceiveBuffer(service).AddRange(partial);

        InvokeParseReceiveBuffer(service);

        Assert.Empty(samples);
        Assert.Equal(partial.Length, ReceiveBuffer(service).Count); // retained for more data
    }

    [Theory]
    [InlineData((byte)0x00)] // count == 0
    [InlineData((byte)0x05)] // count > MAX_SENSORS (4)
    public void ParseReceiveBuffer_InvalidSensorCount_Resyncs(byte badCount)
    {
        var (service, samples) = NewService();
        using var _ = service;
        // Bad header followed by a genuine frame; the parser must resync to it.
        ReceiveBuffer(service).Add(0xAA);
        ReceiveBuffer(service).Add(badCount);
        ReceiveBuffer(service).AddRange(SimpleFrame(0x03, 1.0f));

        InvokeParseReceiveBuffer(service);

        var sample = Assert.Single(samples);
        Assert.Equal("0x03", sample.SensorId);
    }

    [Fact]
    public void ParseReceiveBuffer_InvalidEndByte_Resyncs()
    {
        var (service, samples) = NewService();
        using var _ = service;
        var broken = SimpleFrame();
        broken[^1] = 0x00; // corrupt END byte
        ReceiveBuffer(service).AddRange(broken);

        InvokeParseReceiveBuffer(service);

        Assert.Empty(samples);
        Assert.Empty(ReceiveBuffer(service)); // no further start byte -> cleared
    }

    [Fact]
    public void ParseReceiveBuffer_BadChecksum_Resyncs()
    {
        var (service, samples) = NewService();
        using var _ = service;
        var frame = SimpleFrame();
        frame[^2] ^= 0xFF; // valid structure, wrong checksum
        ReceiveBuffer(service).AddRange(frame);

        InvokeParseReceiveBuffer(service);

        Assert.Empty(samples);
        Assert.Empty(ReceiveBuffer(service));
    }

    [Fact]
    public void ParseReceiveBuffer_ParsesTwoBackToBackFrames()
    {
        var (service, samples) = NewService();
        using var _ = service;
        ReceiveBuffer(service).AddRange(SimpleFrame(0x01, 1f));
        ReceiveBuffer(service).AddRange(SimpleFrame(0x02, 2f));

        InvokeParseReceiveBuffer(service);

        Assert.Equal(2, samples.Count);
        Assert.Equal("0x01", samples[0].SensorId);
        Assert.Equal("0x02", samples[1].SensorId);
        Assert.Empty(ReceiveBuffer(service));
    }

    [Fact]
    public void ParseReceiveBuffer_KeepsTrailingPartialAfterCompleteFrame()
    {
        var (service, samples) = NewService();
        using var _ = service;
        ReceiveBuffer(service).AddRange(SimpleFrame());
        ReceiveBuffer(service).Add(0xAA); // start of a next, not-yet-complete frame

        InvokeParseReceiveBuffer(service);

        Assert.Single(samples);
        Assert.Equal(new byte[] { 0xAA }, ReceiveBuffer(service).ToArray());
    }

    [Fact]
    public void ParseReceiveBuffer_FrameAssemblyTimeout_ResetsParserState()
    {
        var (service, samples) = NewService();
        using var _ = service;
        // A start byte plus a header but an incomplete body, first seen long ago.
        ReceiveBuffer(service).AddRange(new byte[] { 0xAA, 0x01, 0x00 });
        SetSawStartByteAt(service, DateTime.UtcNow.AddSeconds(-1)); // older than the 350 ms timeout

        InvokeParseReceiveBuffer(service);

        Assert.Empty(samples);
        Assert.Empty(ReceiveBuffer(service));
    }

    // ----- ProcessIncomingDataAsync ---------------------------------------

    [Fact]
    public async Task ProcessIncomingDataAsync_ParsesQueuedBytesThenStopsOnCancel()
    {
        var (service, samples) = NewService();
        using var _ = service;
        Enqueue(service, SimpleFrame(0x04, 9.0f));

        using var cts = new CancellationTokenSource();
        var loop = InvokeProcessIncomingDataAsync(service, cts.Token);

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(2))
        {
            lock (samples) { if (samples.Count > 0) break; }
            await Task.Delay(10);
        }

        cts.Cancel();
        await loop; // OperationCanceledException is handled inside the method

        var sample = Assert.Single(samples);
        Assert.Equal("0x04", sample.SensorId);
        Assert.Equal(9.0f, sample.Value);
    }
}
