using System;
using System.Collections.Generic;


public class ResourceMonitor(List<IResource> resources)
{
    private readonly List<IResource> resources = resources;

    public string GetLogs()
    {
        List<string> log = [];

        foreach (var resource in resources)
            log.Add(resource.GetResourceValue());
        
        return string.Join(" | ", log);
    }
}

public interface IResource
{
    public string GetResourceValue();

    public class Value(PerformanceCountManager manager) : IResource
    {
        private readonly PerformanceCountManager _manager = manager;
        public string GetResourceValue()
        {
            _manager.GetUsage();
            return _manager.GetLogMessage();
        }
    }

    public class ThresholdedValue(
        PerformanceCountManager manager, 
        float threshold, 
        Action<float, float> sendAction
        ) : IResource
    {
        private readonly PerformanceCountManager _manager = manager;
        public string GetResourceValue()
        {
            _manager.GetUsage();
            _manager.CheckThereshold(threshold, sendAction);
            return _manager.GetLogMessage();
        }
    }
}



