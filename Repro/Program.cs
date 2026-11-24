using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using HarmonyLib;
using MonoDetour.Interop.HarmonyX;
using TargetLib;

// MonoDetour/MonoMod/HarmonyX versions do not match exactly; resolve by simple name.
AssemblyLoadContext.Default.Resolving += (ctx, name) =>
{
    var loaded = AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(a => a.GetName().Name == name.Name);
    if (loaded != null)
        return loaded;

    var path = Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll")
        .FirstOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f), name.Name, StringComparison.OrdinalIgnoreCase));
    return path is null ? null : ctx.LoadFromAssemblyPath(path);
};

HarmonyXInterop.Initialize();

var harmony = new Harmony("repro");
harmony.PatchAll(typeof(GatherPatch));
Console.WriteLine("patched");

var result = Target.Gather("original").GetAwaiter().GetResult();
Console.WriteLine($"result: {result}");

[HarmonyPatch(typeof(Target), nameof(Target.Gather))]
static class GatherPatch
{
    static bool Prefix(ref ValueTask<string> __result)
    {
        __result = new ValueTask<string>("patched");
        return false;
    }
}
