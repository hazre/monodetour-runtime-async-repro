# MonoDetour HarmonyX interop + runtime-async = CLR segfault

Repro for a .NET 11 CLR crash when MonoDetour's HarmonyX interop is active and HarmonyX patches a runtime-async method.

## Requirements

- .NET 11 SDK
- HarmonyX with runtime-async support (the `runtime-async` branch / PR)
- MonoDetour 0.7.18 (`MonoDetour`, `MonoDetour.Interop.HarmonyX`)

## Run

```bash
dotnet run --project Repro/Repro.csproj -c Release \
  -p:HarmonyXProject=/path/to/HarmonyX/Harmony/Harmony.csproj
```

Expected:

```
patched
result: patched
```

Actual: the process dies with SIGSEGV before `patched` is printed.

`dmesg`/`journalctl -k`:

```
segfault at 0 ip ... in libcoreclr.so[143b1c]
```

## Findings

- Removing `HarmonyXInterop.Initialize()` makes the repro pass.
- The prefix can be a no-op (`static bool Prefix() => true;`); the crash still happens.
- The target must be runtime-async (`MethodImplAttributes` `0x2000`) and await a `ValueTask<T>`.
  A target that only awaits `Task.Yield()` does not crash.
- Managed stack at the crash (from the game's core dump, same signature):

```
RuntimeHelpers.PrepareMethod
FxCoreBaseRuntime.Compile
PlatformTriple.Compile
DMD<...ManagedDetourState::UpdateEndOfChain>
ManagedDetourState.AddILHook
ILHook.Apply
ManagedMethodPatcher.DetourTo
PatchFunctions.UpdateWrapper
PatchClassProcessor.Patch
Harmony.PatchAll
...
```

- The JIT is compiling the generated IL clone (`DMD<>?...`) of the runtime-async method and dereferences a null `TypeHandle` in `TypeHandle::IsValueType`.
- `HarmonyXInterop` ILHooks `HarmonyManipulator.WritePrefixes`, `WritePostfixes`, and `MakeReturnLabel`, i.e. the code that generates exactly this clone.

## Versions

- MonoDetour 0.7.18.0
- HarmonyX 2.16.1 + [runtime-async PR](https://github.com/ResoniteModding/HarmonyX/pull/2)
- .NET 11.0.0, Linux x64
