# Stationeers Terraforming Mod: Development Roadmap

This document outlines the immediate stabilization tasks and proposes advanced gameplay enhancements focused on the ultimate goal: **transforming a hostile planet into a breathable, livable ecosystem.**

---

## Phase 1: Core Stabilization & SLP Migration (Immediate)
*These tasks ensure the mod is stable, performant, and ready for public release.*

* [ ] **Expand Config Settings:** * **Terraforming Difficulty Multiplier:** Expose `worldSize` so players can choose between "God Mode" (instant testing) and "Generational" (7,000,000 size, requiring massive server factories).
    * **Thermodynamic Mass Penalty:** Expose `baseTQ` so admins can adjust how much raw atmospheric mass suppresses global temperatures.
    * **Condensation Toggle:** Keep the `DisableOutdoorCondensation` toggle for server optimization.
* [ ] **Supercooled Gas Persistence:** Verify that the `FreezeWorldAtmosphere` bypass successfully leaves gas in the sky at all temperature ranges without evaporating the mass.
* [ ] **Chunk Boundary Testing:** Drive a rover across a newly generated map grid to ensure the `CloneGlobalGasMix` patch applies the custom atmosphere seamlessly without localized pressure popping.

---

## Phase 2: Visuals & Immersion (The "Looking Up" Phase)
*Now that the math works, players need to see the fruits of their labor without constantly staring at a tablet.*

* [ ] **Dynamic Skybox Tinting:** * Hook into the game's lighting/skybox renderer (e.g., `LightManager` or `SkyManager`).
    * Blend the sky color dynamically based on gas ratios:
        * High Oxygen/Nitrogen = Shift towards Earth-like blue scattering.
        * High Pollutant = Toxic green/brown haze.
        * High Carbon Dioxide = Reddish/orange tint.
* [ ] **Atmospheric Density & Fog:**
    * Tie global pressure and Water Vapor (Steam) to volumetric fog density.
    * A thicker, warmer atmosphere should obscure distant mountains naturally.
* [ ] **Solar Irradiance Scaling:**
    * Patch `OrbitalSimulation.CalculateSolarIrradiance`.
    * As the atmosphere gets thicker (higher pressure/density), solar panel efficiency should slightly decrease due to cloud cover and atmospheric scattering, balancing the endgame power curve.

---

## Phase 3: The "Livable" Ecosystem (Endgame Gameplay)
*Mechanics that reward the player for successfully terraforming the planet.*

* [ ] **Weather Modification:**
    * Hook the `WeatherManager`.
    * If global pressure is high enough and temperature is stabilized, replace default harsh dust storms with milder wind events.
    * *Stretch Goal:* If the atmosphere holds enough Water/Steam and the temp drops, spawn rain or snow particle effects.

---

## Phase 4: Custom Items & UI (Quality of Life)
*Giving players the tools to interact with your new systems.*

* [ ] **Terraforming Tracker Cartridge:** * Create a custom Tablet Cartridge that reads `TerraformingFunctions.ThisGlobalPrecise`.
    * UI displays a progress bar for "Livable Index", showing current Greenhouse Multiplier, global temperature trends, and estimated time to breathable atmosphere.
* [ ] **Mega-Scale Atmospheric Vents:**
    * Standard volume pumps max out at 100L. Create a custom "Terraforming Array" machine (a massive 3x3x3 outdoor vent).
    * Requires vast amounts of power but moves 10,000L of gas per tick directly into the global atmosphere, explicitly designed for endgame terraforming factories.