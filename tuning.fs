/// <summary>
/// The central home for all the hand tuned balance values in the mod.
///
/// Everything here shifts the game's balance away from vanilla: how much of each
/// faction's economy survives, how large the Xenon fleets are, what resources the
/// relocated factions receive, and how much salvage is scattered around the galaxy.
/// Tweak here, regenerate the mod, and every module picks up the new values.
///
/// Grouped by type: Economy (god.xml product quotas), Jobs (fleet sizes),
/// Resources (mining fields and resource areas), GateDefence (bastion stations),
/// and AbandonedShips (claimable wrecks and battlefields).
/// </summary>
module X4.Tuning


// ===== FACTION ECONOMY =====
// Scaling of the god.xml 'product' quotas that determine how many production
// factories each faction gets at game start.
module Economy =
    // only about a fifth of each faction's production factories will be left
    let ProductionRatio = 0.20

    // Xenon get 3 times as many solar factories as vanilla. So can, in theory, build much faster than before
    let XenonProductionRatio = 3

    // Every faction keeps at least this many dynamic factories per product, even after
    // the X4 9.0 prefab factory offset is subtracted (see X4.God.processProduct).
    let MinimumProductQuota = 1


// ===== JOBS (FLEET SIZES) =====
// Multipliers applied to the vanilla job quotas in jobs.xml.
module Jobs =
    // Xenon quota multipliers by ship class.
    let XenonMilitaryXLMultiplier = 2.4 // battleships and carriers
    let XenonMilitaryLMultiplier = 2.4 // destroyers: of which Xenon should have none in vanilla
    let XenonMilitarySMMultiplier = 3.5 // S and M military ships
    let XenonCivilianMultiplier = 5.0 // s & m civilian ships - High number cranks up the Xenon economy. We want them printing ships.

    // Reduce the number of pirates, as they're much more dangerous to the weakened economies.
    let PirateMilitaryMultiplier = 0.4


// ===== RESOURCES (MINING FIELDS) =====
// What mining resources the relocated factions get in their new home sectors.
// Each abstract resource name ("minerals", "ice", ...) grants BOTH a visual field
// region (resourceMap) and, since X4 9.0, the per-sector resource areas that fill
// it with minable yield (resourceAreaMap). The two maps must agree on wares.
module Resources =
    // lookup map to the region definitions from the XML that we will use to place extra resources
    // for factions now in sectors without resources.
    // As of X4 9.0 these regions provide only the VISUAL asteroid/gas fields; the minable
    // yields inside them come from the per-sector resource areas in resourceAreaMap below.
    let resourceMap = Map [
        "minerals", "atf_asteroid_field_high";     // ore, silicon and a little bit of nvidium
        "ice",      "atf_ice_field_high";          // ice
        "scrap",    "atf_wreckfield_xenon_battle"; // scrap
        "hydrogen", "atf_hydrogen_highyield_field";
        "helium",   "atf_helium_highyield_field";
        "methane",  "atf_methane_highyield_field"
    ]

    // One kind of 9.0 'resource area' to add to a sector: composed into a mapdefaults
    // ref of the form sphere_{size}_{ware}_{yieldTier}_{speed}, validated against the
    // vocabulary in libraries/regionyields.xml (see X4.Data.makeResourceAreaRef).
    type ResourceArea = {
        size: string      // tiny / small / medium / large / huge
        ware: string      // ore, silicon, ice, nividium, hydrogen, helium, methane, rawscrap, rawkhaakscrap
        yieldTier: string // verylow / low / medium / high / veryhigh ('yield' is an F# keyword)
        speed: string     // gather speed: veryslow / slow / average / fast / veryfast
        amount: int       // how many areas of this kind the sector gets
    }

    // X4 9.0: the minable resources each abstract resource name grants to a sector.
    // The mix (a few large, rich, slow areas plus smaller average ones) mirrors what
    // vanilla 9.0 gives resource-rich sectors, sized to keep rough parity with the old
    // high-yield atf_* region definitions.
    // INVARIANT: the wares listed for a resource here must be covered by the visual
    // fields of the region resourceMap assigns for the same name - regions without
    // matching resource areas give empty asteroids, and resource areas without a
    // matching region field have nothing to materialise in.
    let resourceAreaMap: Map<string, ResourceArea list> = Map [
        "minerals", [
            { size = "large"; ware = "ore";      yieldTier = "high";   speed = "slow";    amount = 3 }
            { size = "small"; ware = "ore";      yieldTier = "medium"; speed = "average"; amount = 4 }
            { size = "large"; ware = "silicon";  yieldTier = "high";   speed = "slow";    amount = 2 }
            { size = "small"; ware = "silicon";  yieldTier = "medium"; speed = "average"; amount = 3 }
            { size = "small"; ware = "nividium"; yieldTier = "medium"; speed = "average"; amount = 1 }
        ]
        "ice", [
            { size = "large"; ware = "ice";      yieldTier = "high";   speed = "slow";    amount = 3 }
            { size = "small"; ware = "ice";      yieldTier = "medium"; speed = "average"; amount = 3 }
        ]
        "scrap", [
            // the old wreckfield gave rawscrap at medhigh yield plus a little hydrogen
            { size = "medium"; ware = "rawscrap"; yieldTier = "high";   speed = "slow";    amount = 3 }
            { size = "small";  ware = "hydrogen"; yieldTier = "medium"; speed = "average"; amount = 2 }
        ]
        "hydrogen", [
            { size = "large";  ware = "hydrogen"; yieldTier = "high";   speed = "slow";    amount = 3 }
            { size = "medium"; ware = "hydrogen"; yieldTier = "medium"; speed = "average"; amount = 3 }
        ]
        "helium", [
            { size = "large";  ware = "helium";   yieldTier = "high";   speed = "slow";    amount = 3 }
            { size = "medium"; ware = "helium";   yieldTier = "medium"; speed = "average"; amount = 3 }
        ]
        "methane", [
            { size = "large";  ware = "methane";  yieldTier = "high";   speed = "slow";    amount = 3 }
            { size = "medium"; ware = "methane";  yieldTier = "medium"; speed = "average"; amount = 3 }
        ]
    ]

    // The standard resources that we'll use to populate the sectors. Two halves, one for each system.
    let standardResourcesGases = [ "hydrogen"; "helium"; "methane" ]
    let standardResourcesOres = [ "minerals"; "minerals"; "ice"; "scrap" ] // 2x minerals to make it more common

    // How far from the sector centre a generated visual mining field may be placed, in metres.
    let FieldPlacementOffsetXZ = 80000
    let FieldPlacementOffsetY = 5000


// ===== GATE DEFENCE STATIONS =====
// The ring of bastion stations placed around each unsafe gate.
module GateDefence =
    let StationsPerGate = 6
    // Distance from the gate in metres. Give them almost overlapping fields of fire for long range plasma.
    let StationDistance = 15000

    // Bastions used to be plain copies of each faction's standard defence station, but
    // 9.0's more dangerous Xenon capital ships were pummelling them. Each bastion is now
    // this many copies of the faction's defence station stacked into a single
    // construction plan (2 = twice the modules: twice the turrets, shields and hull).
    let BastionStrengthMultiplier = 2
    // Vertical clearance in metres between the physical hulls of stacked copies. The
    // lift applied to each copy is the plan's physical vertical span (module anchor
    // positions extended by each module's body, measured from its hull hardpoint
    // geometry) plus this value, so plans of any height get the same daylight between
    // decks. The hardpoint-derived extents are a lower bound on the real meshes, so
    // keep this generous.
    let BastionCopyClearance = 700.0

    // Equipment fill level (0..1) written to a bastion's <loadout><level> - applied
    // to HATIKVAH bastions only (see god.fs bastionLoadoutLevel); everyone else
    // inherits their vanilla defence entry's loadout. Hatikvah's entry has no level
    // at all, and such stations spawn with the game's sparse default fit -
    // near-unarmed turret hardpoints. The defence plans hardcode no weapons
    // themselves, so this level is what arms the bastion. 1.0 = fully fitted.
    let BastionLoadoutLevel = 0.8

    // Equipment ware ownership granted to factions via a wares.xml diff, as
    // (faction, ware id) pairs. The game fills a <loadout> level only with wares the
    // owning faction has in wares.xml, and hatikvah's vanilla list holds a single L
    // combat turret - the pulse laser - so without a grant even a full loadout mounts
    // nothing stronger. Plasma only, deliberately not beam.
    let FactionWareGrants = [
        "hatikvah", "turret_arg_l_plasma_01_mk1"
    ]


// ===== ABANDONED SHIPS =====
// How many claimable wrecks are scattered around the galaxy, and where.
module AbandonedShips =
    // Ships placed in random UNSAFE (mostly Xenon) sectors.
    let UnsafeMilitaryXL = 4
    let UnsafeMilitaryL = 6
    let UnsafeMilitaryM = 6
    let UnsafeMilitaryS = 6
    let UnsafeEconomyXL = 3
    let UnsafeEconomyL = 12
    let UnsafeEconomyM = 8
    let UnsafeEconomyS = 6

    // Battlefields: clusters of military wrecks near a single point in an unsafe sector.
    // Each entry is the number of (XL, L, M, S) ships in one battlefield.
    let Battlefields = [
        1, 3, 2, 2
        0, 3, 3, 0
        0, 1, 3, 4
        0, 0, 3, 6
        0, 0, 6, 3
    ]

    // Ships placed in random SAFE sectors, for an accessible early game salvage start.
    let SafeMilitaryM = 5
    let SafeEconomyM = 6
    let SafeMilitaryS = 6
    let SafeEconomyS = 8
    let SafeEconomyL = 2

    // Random placement ranges within a sector, in KM offset from the sector centre.
    let SectorScatterX = 160
    let SectorScatterY = 10
    let SectorScatterZ = 180

    // Battlefield wrecks are clustered within this many KM of the battlefield's centre point.
    let BattlefieldSpread = 5
