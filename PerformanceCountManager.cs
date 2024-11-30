using System;
using System.Diagnostics;
using System.Threading;

public abstract class PerformanceCountManager
{
    protected float _lastValue = 0f;

    public virtual bool CheckThereshold(float thresholdValue, Action<float, float> action)
    {
        if (_lastValue > thresholdValue)
            action.Invoke(thresholdValue, _lastValue);

        return _lastValue > thresholdValue;
    }
    abstract public float GetUsage();

    abstract public string GetLogMessage();
}

class CPUCounter(PerformanceCounter cpuCounter) : PerformanceCountManager()
{
    private readonly PerformanceCounter cpuCounter = cpuCounter;

    public override string GetLogMessage() => $"CPU {_lastValue}%";

    public override float GetUsage()
    {
        // Первый вызов NextValue() может вернуть 0, поэтому вызываем дважды с задержкой
        cpuCounter.NextValue();
        Thread.Sleep(1000);
        _lastValue = cpuCounter.NextValue();
        return _lastValue;
    }

}

class RamCounter(PerformanceCounter ramCounter) : PerformanceCountManager
{
    private readonly PerformanceCounter ramCounter = ramCounter;

    public override string GetLogMessage() => $"Память: {_lastValue}MB";

    public override float GetUsage()
    {
        _lastValue = ramCounter.NextValue();
        return _lastValue;
    }

    public override bool CheckThereshold(float thresholdValue, Action<float, float> action)
    {
        if (_lastValue < thresholdValue)
            action.Invoke(thresholdValue, _lastValue);

        return _lastValue > thresholdValue;
    }
}

class DiskCounter(PerformanceCounter diskCounter) : PerformanceCountManager
{
    private readonly PerformanceCounter diskCounter = diskCounter;

    public override string GetLogMessage() => $"Диск: {_lastValue}%";

    public override float GetUsage()
    {
        diskCounter.NextValue();
        Thread.Sleep(1000);
        _lastValue = diskCounter.NextValue();
        return _lastValue;
    }
}

class NetworkCounter(PerformanceCounterCategory networkCounter) : PerformanceCountManager
{
    private readonly string[] networkInterfaces = networkCounter.GetInstanceNames();

    public override string GetLogMessage() => $"Сеть:  {_lastValue / 1024 / 1024} MB/s";

    public override float GetUsage()
    {
        float totalBytesSent = 0;
        float totalBytesReceived = 0;

        foreach (var nic in networkInterfaces)
        {
            using var bytesSentCounter = new PerformanceCounter(
                "Network Interface",
                "Bytes Sent/sec",
                nic
                );
            using var bytesReceivedCounter = new PerformanceCounter(
                "Network Interface",
                "Bytes Received/sec",
                nic
                );
            totalBytesSent += bytesSentCounter.NextValue();
            totalBytesReceived += bytesReceivedCounter.NextValue();
        }

        // Возвращаем суммарную сетевую активность в байтах за секунду
        _lastValue = totalBytesSent + totalBytesReceived;
        return _lastValue;
    }
}

