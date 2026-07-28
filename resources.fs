/// Responsible for adding resources to select sectors for each faction.
/// We add just a few resources as required for the factions to have a basic working economy in
/// the sectors they have been moved to.
///
/// As of X4 9.0 this is a two part job, driven by the same per-sector assignments:
/// 1. VISUALS: place our custom region definitions (region_definitions.xml) as CLUSTER
///    connections in the clusters file. Regions use global positioning rather than
///    referring to the sector directly. These provide the physical asteroid/gas fields.
/// 2. MINABLES: add <resourcearea> entries to each sector's dataset in
///    libraries/mapdefaults.xml. These are invisible yield volumes that roam within the
///    sector's regions and fill their fields with minable resources. Without them the
///    asteroids placed in step 1 are empty; without step 1 they have nothing to fill.
///
/// This module only DECIDES what goes where: it produces RegionPlacement and
/// SectorResourceGrant directives, and the data layer (X4.Data.Regions) writes the XML.

module X4.Resources

open X4.Tuning.Resources // resourceMap, resourceAreaMap and field placement offsets
open X4.Territories
open X4.Types
open X4.Data
open System

let rand = new Random(12345) // Seed the random number generator so we get the same results each time, as long as we're not adding new regions or changing territory order.

// Decide where a visual mining field region goes: randomly place the region in the sector,
// offseting it between -80km to +80 in the x,z coordinates, and up to 5 km in the y coordinate.
let placeRegion cluster sector resource (count: int) : RegionPlacement =
    let x, y, z = getSectorPosition sector

    let x, y, z =
        x + rand.Next(-FieldPlacementOffsetXZ, FieldPlacementOffsetXZ),
        y + rand.Next(-FieldPlacementOffsetY, FieldPlacementOffsetY),
        z + rand.Next(-FieldPlacementOffsetXZ, FieldPlacementOffsetXZ)

    let region = resourceMap.[resource]
    let regionName = $"{sector}_region_{resource}_{count}"
    printfn "%s     Cluster: %s:%s,  Resource: %s:%s @ %A" regionName cluster sector resource region (x, y, z)

    {
        Name = regionName
        Cluster = cluster
        RegionRef = region
        Position = (x, y, z)
    }


// Turn a list into an infinite sequence that cycles through the list from the start once you reach the end
let cycle (data: string list) =
    Seq.initInfinite (fun i -> data.[i % data.Length])

// Compute the (cluster, sector, resource) assignments for a DLC. This is the single
// source of truth shared by the visual region diffs (clusters file) and the 9.0
// mapdefaults resource area diffs, so the visible fields and the minable resource
// areas always land in the same sectors.
let computeResourceAssignments (dlc: string) = [
    for territory in (dlcTerritories dlc) do
        // extract each cluster and list of resource definition from territories, then look up the sectors in the cluster
        // 'sectors' is an infinite sequence that will start again at the first sector in the cluster when it reaches the end
        let sectors =
            getFactionSectorsInCluster territory.faction territory.cluster |> cycle
        // iterate through the resources, and cycle through the sectors in the cluster,
        // assigning each resource to a sector in a round robin fashion
        yield!
            Seq.zip sectors territory.resources
            |> Seq.map (fun (sector, resource) -> (territory.cluster, sector, resource))
            |> Seq.toList
]

// Generate the visual mining field regions: one cluster connection per assignment,
// placed at a seeded-random offset around the sector centre. IMPORTANT: this loop is
// the only consumer of 'rand' above; keep the call sequence stable so region positions
// don't churn between runs.
let generateDLCVisualRegions (dlc: string) (filename: string) assignments =
    printfn "======= Generating visual mining regions for DLC: %s" dlc

    assignments
    |> List.mapi (fun i (cluster, sector, resource) -> placeRegion cluster sector resource (i + 1))
    |> writeClusterRegions dlc filename


// ==== 9.0 mapdefaults resource areas ====

// Merge the resource area lists of all the resources assigned to one sector, summing
// the amounts of identical area kinds. Needed because the round robin wraps around:
// a single sector cluster assigned 'minerals' twice should get double the areas in
// one <add> operation, not two competing operations.
let mergeResourceAreas (resources: string list) =
    resources
    |> List.collect (fun resource -> resourceAreaMap.[resource])
    |> List.groupBy (fun area -> { area with amount = 0 })
    |> List.map (fun (kind, areas) -> { kind with amount = areas |> List.sumBy (fun a -> a.amount) })

// Decide the resource areas a sector receives: the merged area list composed into
// mapdefaults refs, plus how the sector appears in the vanilla file (which the writer
// uses to pick the right diff operation).
let sectorResourceGrant (dlc: string) (sector: string) (resources: string list) : SectorResourceGrant = {
    State = getMapDefaultsDatasetState dlc sector
    Areas =
        mergeResourceAreas resources
        |> List.map (fun area -> makeResourceAreaRef area.size area.ware area.yieldTier area.speed, area.amount)
}

let computeDLCMapDefaultGrants (dlc: string) assignments =
    printfn "======= Computing mapdefaults resource areas for DLC: %s" dlc

    // One grant per sector: gather every resource assigned to the sector together,
    // so the NoDataset case can't produce two competing <dataset> nodes.
    let bySector =
        assignments
        |> List.groupBy (fun (_, sector, _) -> sector)
        |> List.map (fun (sector, entries) -> sector, [ for (_, _, resource) in entries -> resource ])

    bySector
    |> List.map (fun (sector, resources) -> sectorResourceGrant dlc sector resources)


let generate_resource_definitions_file () =
    // Each DLC has a completely different naming scheme for the cluster file.
    // The file *must* be named the same as the dlc, otherwise the patching will fail.
    let dlcClusterFiles = [
        "core", "maps/xu_ep2_universe/clusters.xml"
        "terran", "maps/xu_ep2_universe/dlc_terran_clusters.xml"
        "boron", "maps/xu_ep2_universe/dlc_boron_clusters.xml"
        "split", "maps/xu_ep2_universe/dlc4_clusters.xml"
        "pirate", "maps/xu_ep2_universe/dlc_pirate_clusters.xml"
    ]

    // The two halves need OPPOSITE file placement. Cluster maps are patched per file
    // path, so a DLC cluster can only be reached from extensions/<dlc>/maps/... in
    // our mod. mapdefaults is the reverse: the game merges every extension's own
    // libraries/mapdefaults.xml into one patchable document and never reads nested
    // extensions/<dlc>/libraries copies - so ALL resource area ops, DLC sectors
    // included, must go in the mod's single core mapdefaults diff. (Verified in
    // game: nested cluster diffs apply, nested mapdefaults diffs are ignored, and
    // the core file's access ops successfully patch Terran DLC datasets.)
    let allGrants = [
        for (dlc, clusterFile) in dlcClusterFiles do
            let assignments = computeResourceAssignments dlc
            generateDLCVisualRegions dlc clusterFile assignments // the physical asteroid/gas fields

            for grant in computeDLCMapDefaultGrants dlc assignments do // the minable yields that fill them
                yield dlc, grant
    ]

    writeMapDefaults (allGrants |> List.map snd)

    // mapdefaults grants only seed at game start, so existing saves need the DLC
    // sector resource areas created at runtime by a run-once MD script. Base game
    // grants are excluded: every existing modded save already received them at game
    // start (the base half always worked), so recreating them would duplicate.
    allGrants
    |> List.filter (fun (dlc, _) -> dlc <> "core")
    |> List.map snd
    |> writeResourceRetrofitMD
