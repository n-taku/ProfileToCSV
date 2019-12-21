using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using System.Text;
using System.IO;

public class ProfileToCSV : MonoBehaviour
{
    [Serializable]
    public class FrameData
    {
        public int frame;
        public HierarchyFrameData hierarchyFrameData = new HierarchyFrameData();
        public CPUFrameData cpuFrameData = new CPUFrameData();
        public MemoryFrameData memoryFrameData = new MemoryFrameData();
        public RenderingFrameData renderingFrameData = new RenderingFrameData();
    }

    [Serializable]
    public class HierarchyFrameData
    {
        public float frameFps;
        public float frameTimeMs;
        public float frameGpuTimeMs;
        public float frameIndex;
        public List<HierarchyItemFrameData> hierarchyFrameData = new List<HierarchyItemFrameData>();
    }

    [Serializable]
    public class HierarchyItemFrameData
    {
        public string itemName;
        public string itemPath;
        public string columnName;
        public string columnObjectName;
        public int columnCalls;
        public float columnGcMemory;
        public float columnSelfTime;
        public string columnSelfPercent;
        public float columnTotalTime;
        public string columnTotalPercent;
    }

    [Serializable]
    public class CPUFrameData
    {
        public float rendering;//ナノ秒
        public float scripts;
        public float physics;
        public float animation;
        public float garbageCollector;
        public float VSync;
        public float globalIllumination;
        public float ui;
        public float others;
    }

    [Serializable]
    public class MemoryFrameData
    {
        public int totalAllocated;
        public int textureMemory;
        public int meshMemory;
        public int materialCount;
        public int objectCount;
        public int totalGCAllocated;
        public int globalIllumination;
        public int gcAllocated;
    }

    [Serializable]
    public class RenderingFrameData
    {
        public int batches;
        public int setPassCall;
        public int triangles;
        public int vertices;
    }

    static List<int> parentsCacheList = new List<int>();
    static List<int> childrenCacheList = new List<int>();

    public static FrameData ProcessFrameData(int frame)
    {
        var ret = new FrameData();
        ret.frame = frame;
        ret.hierarchyFrameData = ProcessHierarchyFrameData(frame);
        ret.cpuFrameData = ProcessCPUFrameData(frame);
        ret.memoryFrameData = ProcessMemoryFrameData(frame);
        ret.renderingFrameData = ProcessRenderingFrameData(frame);
        return ret;
    }

    public static HierarchyFrameData ProcessHierarchyFrameData(int frame)
    {
        var f = new HierarchyFrameData();
        using (var frameData = ProfilerDriver.GetHierarchyFrameDataView(frame, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnGcMemory, false))
        {
            f.frameFps = frameData.frameFps;
            f.frameTimeMs = frameData.frameTimeMs;
            f.frameGpuTimeMs = frameData.frameGpuTimeMs;
            f.frameIndex = frameData.frameIndex;

            int rootId = frameData.GetRootItemID();
            frameData.GetItemDescendantsThatHaveChildren(rootId, parentsCacheList);
            foreach (int parentId in parentsCacheList)
            {
                frameData.GetItemChildren(parentId, childrenCacheList);
                foreach (var child in childrenCacheList)
                {
                    var h = new HierarchyItemFrameData();
                    h.itemName = frameData.GetItemName(child);
                    h.itemPath = frameData.GetItemPath(child);
                    h.columnName = frameData.GetItemColumnData(child, HierarchyFrameDataView.columnName);
                    h.columnObjectName = frameData.GetItemColumnData(child, HierarchyFrameDataView.columnObjectName);
                    h.columnCalls = (int)frameData.GetItemColumnDataAsSingle(child, HierarchyFrameDataView.columnCalls);
                    h.columnGcMemory = frameData.GetItemColumnDataAsSingle(child, HierarchyFrameDataView.columnGcMemory);
                    h.columnSelfTime = frameData.GetItemColumnDataAsSingle(child, HierarchyFrameDataView.columnSelfTime);
                    h.columnSelfPercent = frameData.GetItemColumnData(child, HierarchyFrameDataView.columnSelfPercent);
                    h.columnTotalTime = frameData.GetItemColumnDataAsSingle(child, HierarchyFrameDataView.columnTotalTime);
                    h.columnTotalPercent = frameData.GetItemColumnData(child, HierarchyFrameDataView.columnTotalPercent);
                    f.hierarchyFrameData.Add(h);
                }
            }
        }
        return f;
    }

    public static CPUFrameData ProcessCPUFrameData(int frame)
    {
        var c = new CPUFrameData();
        var statistics = ProfilerDriver.GetGraphStatisticsPropertiesForArea(ProfilerArea.CPU);
        foreach (var propertyName in statistics)
        {
            var id = ProfilerDriver.GetStatisticsIdentifierForArea(ProfilerArea.CPU, propertyName);
            var buffer = new float[1];
            ProfilerDriver.GetStatisticsValues(id, frame, 1, buffer, out var maxValue);
            if (propertyName == "Rendering") c.rendering = buffer[0];
            else if (propertyName == "Scripts") c.scripts = buffer[0];
            else if (propertyName == "Physics") c.physics = buffer[0];
            else if (propertyName == "Animation") c.animation = buffer[0];
            else if (propertyName == "GarbageCollector") c.garbageCollector = buffer[0];
            else if (propertyName == "VSync") c.VSync = buffer[0];
            else if (propertyName == "Global Illumination") c.globalIllumination = buffer[0];
            else if (propertyName == "UI") c.ui = buffer[0];
            else if (propertyName == "Others") c.others = buffer[0];

            Debug.Log(ProfilerDriver.GetFormattedStatisticsValue(frame, id));
        }

        return c;
    }

    public static MemoryFrameData ProcessMemoryFrameData(int frame)
    {
        var m = new MemoryFrameData();
        var statistics = ProfilerDriver.GetGraphStatisticsPropertiesForArea(ProfilerArea.Memory);
        foreach (var propertyName in statistics)
        {
            var id = ProfilerDriver.GetStatisticsIdentifierForArea(ProfilerArea.Memory, propertyName);
            var buffer = new float[1];
            ProfilerDriver.GetStatisticsValues(id, frame, 1, buffer, out var maxValue);
            if (propertyName == "Total Allocated") m.totalAllocated = (int)buffer[0];
            else if (propertyName == "Texture Memory") m.textureMemory = (int)buffer[0];
            else if (propertyName == "Mesh Memory") m.meshMemory = (int)buffer[0];
            else if (propertyName == "Material Count") m.materialCount = (int)buffer[0];
            else if (propertyName == "Object Count") m.objectCount = (int)buffer[0];
            else if (propertyName == "Total GC Allocated") m.totalGCAllocated = (int)buffer[0];
            else if (propertyName == "Global Illumination") m.globalIllumination = (int)buffer[0];
            else if (propertyName == "GC Allocated") m.gcAllocated = (int)buffer[0];
        }

        return m;
    }

    public static RenderingFrameData ProcessRenderingFrameData(int frame)
    {
        var r = new RenderingFrameData();
        var statistics = ProfilerDriver.GetGraphStatisticsPropertiesForArea(ProfilerArea.Rendering);
        foreach (var propertyName in statistics)
        {
            var id = ProfilerDriver.GetStatisticsIdentifierForArea(ProfilerArea.Rendering, propertyName);
            var buffer = new float[1];
            ProfilerDriver.GetStatisticsValues(id, frame, 1, buffer, out var maxValue);
            if (propertyName == "Batches") r.batches = (int)buffer[0];
            else if (propertyName == "SetPass Calls") r.setPassCall = (int)buffer[0];
            else if (propertyName == "Triangles") r.triangles = (int)buffer[0];
            else if (propertyName == "Vertices") r.vertices = (int)buffer[0];
        }
        return r;
    }

    [MenuItem("Custom/Test")]
    public static void Test()
    {
        var statistics = ProfilerDriver.GetGraphStatisticsPropertiesForArea(ProfilerArea.CPU);
        foreach (var propertyName in statistics)
        {
            var id = ProfilerDriver.GetStatisticsIdentifierForArea(ProfilerArea.CPU, propertyName);
            Debug.Log($"{propertyName} : {id}");
        }

        statistics = ProfilerDriver.GetGraphStatisticsPropertiesForArea(ProfilerArea.Memory);
        foreach (var propertyName in statistics)
        {
            var id = ProfilerDriver.GetStatisticsIdentifierForArea(ProfilerArea.Memory, propertyName);
            Debug.Log($"{propertyName} : {id}");
        }

        statistics = ProfilerDriver.GetGraphStatisticsPropertiesForArea(ProfilerArea.Rendering);
        foreach (var propertyName in statistics)
        {
            var id = ProfilerDriver.GetStatisticsIdentifierForArea(ProfilerArea.Rendering, propertyName);
            Debug.Log($"{propertyName} : {id}");
        }
    }

    [MenuItem("Custom/StartProfile")]
    public static void StartProfile()
    {
        Profiler.logFile = "data.log.raw";
        Profiler.enableBinaryLog = true;
        Profiler.enabled = true;
        Profiler.SetAreaEnabled(ProfilerArea.CPU, true);
        Profiler.SetAreaEnabled(ProfilerArea.Memory, true);
        Profiler.SetAreaEnabled(ProfilerArea.Rendering, true);
    }

    [MenuItem("Custom/EndProfile")]
    public static void EndProfile()
    {
        Profiler.logFile = "";
        Profiler.enabled = false;
    }

    [MenuItem("Custom/Analyze")]
    public static void Analyze()
    {
        ProfilerDriver.LoadProfile("data.log.raw", true);

        List<FrameData> frameDatas = new List<FrameData>();
        var first = ProfilerDriver.firstFrameIndex;
        var last = ProfilerDriver.lastFrameIndex;
        for (int i = first; i < last; i++)
        {
            var frameData = ProcessFrameData(i);
            frameDatas.Add(frameData);
        }

        CPUFrameDataToCSV(frameDatas);
        MemoryFrameDataToCSV(frameDatas);
        RenderingFrameDataToCSV(frameDatas);
        HierarchyFrameDataToCSV(frameDatas);
        HierarchyItemFrameDataToCSV(frameDatas);
    }

    static void CPUFrameDataToCSV(List<FrameData> frameDatas)
    {
        var sb = new StringBuilder();
        sb.AppendLine("frame,rendering,scripts,physics,animation,garbageCollector,VSync,globalIllumination,ui,others");
        foreach (var frameData in frameDatas)
        {
            var frame = frameData.frame;
            var d = frameData.cpuFrameData;
            sb.AppendLine($"{frame},{d.rendering},{d.scripts},{d.physics},{d.animation},{d.garbageCollector},{d.VSync},{d.globalIllumination},{d.ui},{d.others}");
        }
        File.WriteAllText("cpu.csv", sb.ToString());
    }

    static void MemoryFrameDataToCSV(List<FrameData> frameDatas)
    {
        var sb = new StringBuilder();
        sb.AppendLine("frame,totalAllocated,textureMemory,meshMemory,materialCount,objectCount,totalGCAllocated,globalIllumination,gcAllocated");
        foreach (var frameData in frameDatas)
        {
            var frame = frameData.frame;
            var d = frameData.memoryFrameData;
            sb.AppendLine($"{frame},{d.totalAllocated},{d.textureMemory},{d.meshMemory},{d.materialCount},{d.objectCount},{d.totalGCAllocated},{d.globalIllumination},{d.gcAllocated}");
        }
        File.WriteAllText("memory.csv", sb.ToString());
    }

    static void RenderingFrameDataToCSV(List<FrameData> frameDatas)
    {
        var sb = new StringBuilder();
        sb.AppendLine("frame,batches,setPassCall,triangles,vertices");
        foreach (var frameData in frameDatas)
        {
            var frame = frameData.frame;
            var d = frameData.renderingFrameData;
            sb.AppendLine($"{frame},{d.batches},{d.setPassCall},{d.triangles},{d.vertices}");
        }
        File.WriteAllText("rendering.csv", sb.ToString());
    }

    static void HierarchyFrameDataToCSV(List<FrameData> frameDatas)
    {
        var sb = new StringBuilder();
        sb.AppendLine("frame,frameIndex,frameFps,frameTimeMs,frameGpuTimeMs");
        foreach (var frameData in frameDatas)
        {
            var frame = frameData.frame;
            var d = frameData.hierarchyFrameData;
            sb.AppendLine($"{frame},{d.frameIndex},{d.frameFps},{d.frameTimeMs},{d.frameGpuTimeMs}");
        }
        File.WriteAllText("hierarchy.csv", sb.ToString());
    }

    static void HierarchyItemFrameDataToCSV(List<FrameData> frameDatas)
    {
        var sb = new StringBuilder();
        sb.AppendLine("frame,frameIndex,itemName,itemPath,columnName,columnObjectName,columnCalls,columnGcMemory,columnSelfTime,columnSelfPercent,columnTotalTime,columnTotalPercent");
        foreach (var frameData in frameDatas)
        {
            var frame = frameData.frame;
            var d = frameData.hierarchyFrameData;
            foreach (var item in d.hierarchyFrameData)
            {
                sb.AppendLine($"{frame},{d.frameIndex},{item.itemName},{item.itemPath},{item.columnName},{item.columnObjectName},{item.columnCalls},{item.columnGcMemory},{item.columnSelfTime},{item.columnSelfPercent},{item.columnTotalTime},{item.columnTotalPercent}");
            }
        }
        File.WriteAllText("hierarchy_item.csv", sb.ToString());
    }
}