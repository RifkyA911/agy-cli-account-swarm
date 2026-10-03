using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace AgyAccountSwarm.Services;

public record ProcessNode(int ProcessId, int ParentProcessId, string Name);

public static class Win32ProcessHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const uint TH32CS_SNAPPROCESS = 0x00000002;

    public static List<ProcessNode> GetAllProcesses()
    {
        var list = new List<ProcessNode>();
        if (!OperatingSystem.IsWindows()) return list;

        var handle = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1)) return list;

        try
        {
            var entry = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            if (Process32First(handle, ref entry))
            {
                do
                {
                    list.Add(new ProcessNode((int)entry.th32ProcessID, (int)entry.th32ParentProcessID, entry.szExeFile));
                }
                while (Process32Next(handle, ref entry));
            }
        }
        finally
        {
            CloseHandle(handle);
        }

        return list;
    }

    public static List<ProcessNode> GetDescendantProcesses(int rootPid, List<ProcessNode>? allProcesses = null)
    {
        var all = allProcesses ?? GetAllProcesses();
        var childrenByParent = all
            .GroupBy(p => p.ParentProcessId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<ProcessNode>();
        var queue = new Queue<int>();
        queue.Enqueue(rootPid);

        var visited = new HashSet<int> { rootPid };

        while (queue.Count > 0)
        {
            var currentPid = queue.Dequeue();
            if (childrenByParent.TryGetValue(currentPid, out var children))
            {
                foreach (var child in children)
                {
                    if (visited.Add(child.ProcessId))
                    {
                        result.Add(child);
                        queue.Enqueue(child.ProcessId);
                    }
                }
            }
        }

        return result;
    }

    public static bool IsParentOrAncestor(int ancestorPid, int childPid, List<ProcessNode>? allProcesses = null)
    {
        var all = allProcesses ?? GetAllProcesses();
        var parentMap = new Dictionary<int, int>();
        foreach (var p in all)
        {
            parentMap[p.ProcessId] = p.ParentProcessId;
        }

        int current = childPid;
        var visited = new HashSet<int>();

        while (parentMap.TryGetValue(current, out int parent))
        {
            if (parent == ancestorPid) return true;
            if (parent == 0 || !visited.Add(parent)) break;
            current = parent;
        }

        return false;
    }
}
