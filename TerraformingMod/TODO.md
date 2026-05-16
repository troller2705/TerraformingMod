# Terraforming Mod - Update TODO List

## 1. Workspace & Architecture
- [x] Verify assembly references point to the latest `Assembly-CSharp.dll`, `UnityEngine.CoreModule.dll`, `0Harmony.dll`, and `BepInEx.dll`.

## 2. Atmospherics & Chemistry API
- [x] **Global Rename:** Change all instances of `GasType.Volatiles` to `GasType.Methane`.
- [x] **Gas Arrays:** Update any hardcoded gas array limits to use `(int)Chemistry.GasType.Count` to support the new 32-gas system.
- [x] **GasMixtures:** Refactor global tick logic to use native `GasMixture.Add()` / `GasMixture.Remove()` directly instead of instantiating new objects.
- [ ] **Events:** Update `AtmosphericEventInstance` logic. It is now a `readonly struct`. Adjust gas payloads via `Prefix` patches *before* the event struct is constructed.
- [ ] **Thermodynamics:** Patch `Atmosphere.AddEnergy` to intercept the new `SolarIrradiance` models and apply your terraforming temperature scaling.

## 3. Multiplayer Synchronization
- [ ] Refactor custom network messages for atmospheric syncing. Ensure they conform to the new Multiplayer Tick assembly protocol (no array double-handling).

## 4. Visuals & Rendering
- [ ] **Important:** Completely remove the custom render class.
- [ ] Hook visual updates (sky color, planet fog) directly into the game's native lighting singletons and terrain/voxel LOD system.

## 5. Testing & Data
- [ ] Migrate planetary base states and start conditions to the new native XML modding framework.
