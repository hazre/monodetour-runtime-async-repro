# MonoDetour runtime-async repro

Minimal repro for a .NET 11 CLR segfault when MonoDetour's HarmonyX interop is active and HarmonyX patches a runtime-async method.

## Layout

- `TargetLib/` — a runtime-async `ValueTask<string>` target (`MethodImplAttributes 0x2000`).
- `Repro/` — initializes `MonoDetour.Interop.HarmonyX.HarmonyXInterop`, then applies a HarmonyX patch to `TargetLib.Target.Gather`.

## Requirements

- .NET 11 SDK
- HarmonyX with runtime-async support (the `runtime-async` branch / PR)
- MonoDetour 0.7.18 (`MonoDetour`, `MonoDetour.Interop.HarmonyX`)

## Run

```bash
dotnet run --project Repro/Repro.csproj -c Release \
  -p:HarmonyXProject=/path/to/HarmonyX/Harmony/Harmony.csproj
```

## Expected

```
patched
result: patched
```

## Actual

The process dies with SIGSEGV before `patched` is printed.

Kernel log:

```
segfault at 0 ip ... in libcoreclr.so[143b1c]
```

Managed stack from the core dump (same signature as the game):

```
MonoMod.Core.Interop.CoreCLR+V60.InvokeCompileMethod(...)
MonoMod.Core.Platforms.Runtimes.Core60Runtime+JitHookDelegateHolder.CompileMethodHook(...)
System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(...)
MonoMod.Core.Platforms.Runtimes.FxCoreBaseRuntime.Compile(MethodBase, RuntimeMethodHandle)
MonoMod.Core.Platforms.PlatformTriple.Compile(MethodBase, RuntimeMethodHandle)
DMD<...ManagedDetourState::UpdateEndOfChain>(ManagedDetourState)
SyncProxy<...UpdateEndOfChain()>(ManagedDetourState)
MonoMod.RuntimeDetour.DetourManager+ManagedDetourState.AddILHook(SingleILHookState, Boolean)
MonoMod.RuntimeDetour.ILHook.Apply()
HarmonyLib.Public.Patching.ManagedMethodPatcher.DetourTo(MethodBase)
HarmonyLib.PatchFunctions.UpdateWrapper(MethodBase, PatchInfo)
HarmonyLib.PatchClassProcessor.ProcessPatchJob(Job<MethodInfo>)
HarmonyLib.PatchClassProcessor.PatchWithAttributes(...)
HarmonyLib.PatchClassProcessor.Patch()
HarmonyLib.Harmony.PatchAll(Type)
Program.<Main>$
```

The JIT is compiling the generated DMD clone of the runtime-async method and dereferences a null `TypeHandle` in `TypeHandle::IsValueType`.

## Findings

- Removing `HarmonyXInterop.Initialize()` makes the repro pass.
- The prefix can be a no-op (`static bool Prefix() => true;`); the crash still happens. It is not caused by prefix logic or `__result` handling.
- The target must be runtime-async (`MethodImplAttributes` `0x2000`) and await a `ValueTask<T>`. A target that only awaits `Task.Yield()` does not crash.
- `HarmonyXInterop` ILHooks `HarmonyManipulator.WritePrefixes`, `WritePostfixes`, and `MakeReturnLabel` — the code that generates the IL clone being compiled.
- MonoDetour 0.7.18 already includes the MonoMod 25.4 IL-analysis fix (`d801177`, "fix MonoDetour's IL analysis for MonoMod 25.4.0"), so this is **not** the `PlatformTriple.Compile(MethodBase, RuntimeMethodHandle)` signature mismatch. `ILHookDMDManipulation` already wraps `DynamicMethodDefinition.Generate()` / `PlatformTriple.Compile()` in a try/catch, but the runtime-async failure is a native CLR segfault, so the managed catch never runs.

## Versions

- MonoDetour 0.7.18
- MonoDetour.Interop.HarmonyX 0.7.18
- HarmonyX 2.16.1 + [runtime-async PR](https://github.com/ResoniteModding/HarmonyX/pull/2)
- .NET 11.0.0, Linux x64

## Workaround

Disable MonoDetour's HarmonyX interop (do not call `HarmonyXInterop.Initialize()` / remove the MonoDetour BepInEx patcher). HarmonyX runtime-async patches then work; MonoDetour's HarmonyX-specific instruction/patch tracking is lost.
