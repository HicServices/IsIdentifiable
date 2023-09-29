using HarmonyLib;
using InteropDotNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace IsIdentifiable;

/// <summary>
/// Work around legacy Tesseract Interop code relying on old libdl.so
/// In an ideal world we'd just implement ILibraryLoaderLogic and pass
/// this to LibraryLoader - but that's locked down as 'internal', so
/// we have to jump through some reflection hoops to get there.
/// </summary>
internal sealed class TesseractLinuxLoaderFix
{
    private static readonly Dictionary<string, IntPtr>? LoadedAssemblies;

    /// <summary>
    /// Install the patch, if running on Linux (NOP on other platforms)
    /// </summary>
    static TesseractLinuxLoaderFix()
    {
        if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return; // Only apply patch on Linux

        var harmony = new Harmony("uk.ac.dundee.hic.tesseract");
        var ll = typeof(LibraryLoader);
        var self = typeof(TesseractLinuxLoaderFix);
        LoadedAssemblies = ll.GetField("loadedAssemblies", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(LibraryLoader.Instance) as Dictionary<string, IntPtr>;
        harmony.Patch(ll.GetMethod("LoadLibrary"), prefix: new HarmonyMethod(self.GetMethod(nameof(LoadLibraryPatch), BindingFlags.NonPublic | BindingFlags.Static)));
        harmony.Patch(ll.GetMethod("GetProcAddress"), prefix: new HarmonyMethod(self.GetMethod(nameof(GetProcAddressPatch), BindingFlags.NonPublic | BindingFlags.Static)));
        harmony.Patch(ll.GetMethod("FreeLibrary"), prefix: new HarmonyMethod(self.GetMethod(nameof(FreeLibraryPatch), BindingFlags.NonPublic | BindingFlags.Static)));
    }

    // NOTE(rkm 2023-06-26) Parameter name required for Harmony LibraryLoader
#pragma warning disable IDE0060 // Remove unused parameter
    private static bool LoadLibraryPatch(string fileName, string platformName, ref IntPtr __result)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        var fullPath = $"{AppDomain.CurrentDomain.BaseDirectory}/runtimes/linux-x64/native/lib{fileName}.so";
        try
        {
            __result = UnixLoadLibrary(fullPath, 2);
        }
        catch (EntryPointNotFoundException)
        {
            __result = DLLoadLibrary(fullPath, 2);
        }

        if (__result == IntPtr.Zero)
            throw new DllNotFoundException($"Failed to load '{fullPath}' - {(File.Exists(fullPath) ? "exists but could not load" : "file missing")}");
        LoadedAssemblies.Add(fileName, __result);
        return false;
    }

    private static bool GetProcAddressPatch(IntPtr dllHandle, string name, ref IntPtr __result)
    {
        try
        {
            __result = UnixGetProcAddress(dllHandle, name);
        }
        catch (EntryPointNotFoundException)
        {
            __result = DLGetProcAddress(dllHandle, name);
        }
        if (__result == IntPtr.Zero)
            Console.Error.WriteLine($"WARN:Failed looking for function '{name}' in module at address {dllHandle}");
        return false;
    }

    private static bool FreeLibraryPatch(string fileName, ref bool __result)
    {
        if (LoadedAssemblies.Remove(fileName, out var handle))
            try
            {
                __result = UnixFreeLibrary(handle) != 0;
            }
            catch (EntryPointNotFoundException)
            {
                __result = DLFreeLibrary(handle) != 0;
            }
        else
        {
            __result = false;
            Console.Error.WriteLine($"WARNING:Ignoring attempt to unload unknown library {fileName}");
        }
        return false;
    }


    [DllImport("dl", EntryPoint = "dlopen")]
    private static extern IntPtr DLLoadLibrary(string fileName, int flags);

    [DllImport("dl", EntryPoint = "dlclose", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int DLFreeLibrary(IntPtr handle);

    [DllImport("dl", EntryPoint = "dlsym", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern IntPtr DLGetProcAddress(IntPtr handle, string symbol);

    [DllImport("dl", EntryPoint = "dlerror", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern IntPtr DLGetLastError();

    [DllImport("c", EntryPoint = "dlopen")]
    private static extern IntPtr UnixLoadLibrary(string fileName, int flags);

    [DllImport("c", EntryPoint = "dlclose", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int UnixFreeLibrary(IntPtr handle);

    [DllImport("c", EntryPoint = "dlsym", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern IntPtr UnixGetProcAddress(IntPtr handle, string symbol);

    [DllImport("c", EntryPoint = "dlerror", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern IntPtr UnixGetLastError();

    public static void Patch()
    {
        // NOP: Just referencing this type forces the static constructor to apply the patch
    }
}
