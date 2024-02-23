/// <summary>
/// This module contains data around factions, sectors and rules we'll use to
/// generate our new universe.
/// </summary>

module X4.Data

open FSharp.Data
open X4.Utilities

[<Literal>]
let X4UnpackedDataFolder = __SOURCE_DIRECTORY__ + "/X4_unpacked_data"


// Neat case insensitive string comparison function from https://stackoverflow.com/questions/1936767/f-case-insensitive-string-compare
// It's important as the X4 data files often mix the case of identifiers like zone and sector names.
let (=?) s1 s2 = System.String.Equals(s1, s2, System.StringComparison.CurrentCultureIgnoreCase)


[<Literal>]
let X4ClusterFileCore = X4UnpackedDataFolder + "/core/maps/xu_ep2_universe/clusters.xml"
let X4ClusterFileSplit = X4UnpackedDataFolder + "/split/maps/xu_ep2_universe/dlc4_clusters.xml"
let X4ClusterFileTerran = X4UnpackedDataFolder + "/terran/maps/xu_ep2_universe/dlc_terran_clusters.xml"
let X4ClusterFilePirate = X4UnpackedDataFolder + "/pirate/maps/xu_ep2_universe/dlc_pirate_clusters.xml"
let X4ClusterFileBoron = X4UnpackedDataFolder + "/boron/maps/xu_ep2_universe/dlc_boron_clusters.xml"

[<Literal>]
let X4SectorFileCore = X4UnpackedDataFolder  + "/core/maps/xu_ep2_universe/sectors.xml" // This core sectors file needs to be a literal, as it's also our type provider
let X4SectorFileSplit = X4UnpackedDataFolder + "/split/maps/xu_ep2_universe/dlc4_sectors.xml"   // This one is normal string, as we can load and parse using X4SectorCore literal
let X4SectorFileTerran = X4UnpackedDataFolder + "/terran/maps/xu_ep2_universe/dlc_terran_sectors.xml" 
let X4SectorFilePirate = X4UnpackedDataFolder + "/pirate/maps/xu_ep2_universe/dlc_pirate_sectors.xml" 
let X4SectorFileBoron = X4UnpackedDataFolder + "/boron/maps/xu_ep2_universe/dlc_boron_sectors.xml"   

[<Literal>]
let X4ZoneFileCore = X4UnpackedDataFolder + "/core/maps/xu_ep2_universe/zones.xml"
let X4ZoneFileSplit = X4UnpackedDataFolder + "/split/maps/xu_ep2_universe/dlc4_zones.xml"
let X4ZoneFileTerran = X4UnpackedDataFolder + "/terran/maps/xu_ep2_universe/dlc_terran_zones.xml"
let X4ZoneFilePirate = X4UnpackedDataFolder + "/pirate/maps/xu_ep2_universe/dlc_pirate_zones.xml"
let X4ZoneFileBoron = X4UnpackedDataFolder + "/boron/maps/xu_ep2_universe/dlc_boron_zones.xml"


[<Literal>]
let X4GalaxyFileCore = X4UnpackedDataFolder + "/core/maps/xu_ep2_universe/galaxy.xml"
[<Literal>]  // the DLC galaxy files are in DIFF format, so we need a different type provider.
let X4GalaxyFileSplit = X4UnpackedDataFolder + "/split/maps/xu_ep2_universe/galaxy.xml"
let X4GalaxyFileTerran = X4UnpackedDataFolder + "/terran/maps/xu_ep2_universe/galaxy.xml"
let X4GalaxyFilePirate = X4UnpackedDataFolder + "/pirate/maps/xu_ep2_universe/galaxy.xml"
let X4GalaxyFileBoron = X4UnpackedDataFolder + "/boron/maps/xu_ep2_universe/galaxy.xml"


type X4Cluster    = XmlProvider<X4ClusterFileCore>
type X4Sector     = XmlProvider<X4SectorFileCore>
type X4Zone       = XmlProvider<X4ZoneFileCore>
type X4Galaxy     = XmlProvider<X4GalaxyFileCore>
// the DLC galaxy files are in DIFF format, so we need a different type provider.
type X4GalaxyDiff = XmlProvider<X4GalaxyFileSplit>


// Load the cluster data from each individual core/expansion cluster XML file. We'll combine them in to one list.
// Convinience functions to search/manipulate these lists are defined below.
let AllClusters =
    let X4ClusterCore   = X4Cluster.Load(X4ClusterFileCore)
    let X4ClusterSplit  = X4Cluster.Load(X4ClusterFileSplit)
    let X4ClusterTerran = X4Cluster.Load(X4ClusterFileTerran)
    let X4ClusterPirate = X4Cluster.Load(X4ClusterFilePirate)
    let X4ClusterBoron  = X4Cluster.Load(X4ClusterFileBoron)
    Array.toList <| Array.concat [
                    X4ClusterCore.Macros;
                    X4ClusterSplit.Macros;
                    X4ClusterTerran.Macros;
                    X4ClusterPirate.Macros;
                    X4ClusterBoron.Macros;
                ]

// Load the sector data from each individual sector file. We'll combine them in to one list.
let allSectors = 
    let X4SectorCore = X4Sector.Load(X4SectorFileCore)
    let X4SectorSplit = X4Sector.Load(X4SectorFileSplit)
    let X4SectorTerran = X4Sector.Load(X4SectorFileTerran)
    let X4SectorPirate = X4Sector.Load(X4SectorFilePirate)
    let X4SectorBoron = X4Sector.Load(X4SectorFileBoron)
    Array.toList <| Array.concat [
                    X4SectorCore.Macros; 
                    X4SectorSplit.Macros; 
                    X4SectorTerran.Macros; 
                    X4SectorPirate.Macros;
                    X4SectorBoron.Macros; 
                ]

let allZones =
    let X4ZoneCore = X4Zone.Load(X4ZoneFileCore)
    let X4ZoneSplit = X4Zone.Load(X4ZoneFileSplit)
    let X4ZoneTerran = X4Zone.Load(X4ZoneFileTerran)
    let X4ZonePirate = X4Zone.Load(X4ZoneFilePirate)
    let X4ZoneBoron = X4Zone.Load(X4ZoneFileBoron)
    Array.toList <| Array.concat [
                    X4ZoneCore.Macros;
                    X4ZoneSplit.Macros;
                    X4ZoneTerran.Macros;
                    X4ZonePirate.Macros;
                    X4ZoneBoron.Macros;
                ]


let allGalaxy =
    // we're assuming that the galaxy file just contains connections, and that the connection fields/structure
    // is pretty much the same between core and DLCs. Otherwise this casting from one to the other using the
    // XElement is dangerous. This only runs on mod creation though, and if it crashes it means something has
    // changed that we need to account for anyway.
    let loadFromDiff (diff:X4GalaxyDiff.Diff) =
        // Galaxy file just contains a list of connections.
        [|  for connection in diff.Add.Connections do
                yield new X4Galaxy.Connection(connection.XElement)
        |]

    let X4GalaxyCore = X4Galaxy.Load(X4GalaxyFileCore)
    let X4GalaxySplit = X4GalaxyDiff.Load(X4GalaxyFileSplit)
    let X4GalaxyTerran = X4GalaxyDiff.Load(X4GalaxyFileTerran)
    let X4GalaxyPirate = X4GalaxyDiff.Load(X4GalaxyFilePirate)
    let X4GalaxyBoron = X4GalaxyDiff.Load(X4GalaxyFileBoron)
    Array.toList <| Array.concat [
                    X4GalaxyCore.Macro.Connections;
                    loadFromDiff X4GalaxySplit;
                    loadFromDiff X4GalaxyTerran;
                    loadFromDiff X4GalaxyPirate;
                    loadFromDiff X4GalaxyBoron;
                ]

// Gates are linked to a zone by using one of the following as a reference. So by looking
// for these references in the zone file, we can find the gates in a zone.
// I don't think we actually need this. More investigation seems to suggest that a gate is
// identified by a zone connection ref="gates" instead. If correct, we can remove this.
let gateMacros = ["props_gates_orb_accelerator_01_macro", "props_gates_anc_gate_macro", "props_ter_gate_01_macro"]

// Factions will be limited to only a sector or two of valid territory.
// We'll put their defence stations and shipyards in these sectors. The game
// will then generate product factories here.
// We also need to place their starting ships in these sectors.
// We need to scan the xml files for what resources are available in these
// sectors; and if it's not enough, add new resources to the sector so that
// the faction has at least a minimal economy.
// Lastly, we'll need to place the Bastion stations near the jumpgates. We
// can also find the location of the jumpgates in the xml files, and then
// place three bastion stations around each one, encircling it.
type Location = { x: float; y: float; z: float }
type MiningResource = { name: string; amount: int; location: Location }
type Territory = { faction: string; cluster:string; sector: string; zone: string; resources: MiningResource list; gates: Location list }
                   static member Default = { faction = ""; cluster = ""; sector = ""; zone = ""; resources = []; gates = [] }

// create a list of all the factions and their territories as Territory records
// We'll pull resources and gates from the xml files, based on the sector name.
// We will likely need to create a zone (or find) in each of these sectors to place the station
let territories = [
    // core
    { Territory.Default with faction = "argon"; sector = "Cluster_14_Sector001_macro" }      // Argon Prime
    { Territory.Default with faction = "argon"; sector = "Cluster_29_Sector002_macro" }      // Hatikvah's choice III
    { Territory.Default with faction = "hatikvah"; sector = "Cluster_29_Sector001_macro" }   // Hatikvah's choice I
    { Territory.Default with faction = "antigone"; sector = "Cluster_27_Sector001_macro" }   // The Void
    { Territory.Default with faction = "antigone"; sector = "Cluster_28_Sector001_macro" }   // Antigone Menorial

    { Territory.Default with faction = "teladi"; sector = "Cluster_15_Sector001_macro" }     // Ianamus Zura IV
    { Territory.Default with faction = "teladi"; sector = "Cluster_15_Sector002_macro" }     // Ianamus Zura VII
    { Territory.Default with faction = "ministry"; sector = "Cluster_15_Sector001_macro" }   // Ianamus Zura IV
    { Territory.Default with faction = "ministry"; sector = "Cluster_15_Sector002_macro" }   // Ianamus Zura VII
//    { faction = "scaleplate"; sector = "" }

    { Territory.Default with faction = "paranid"; sector = "Cluster_47_Sector001_macro" }   // Trinity Sanctum VII
    { Territory.Default with faction = "paranid"; sector = "Cluster_18_Sector001_macro" }   // Trinity Sanctum III
    { Territory.Default with faction = "alliance"; sector = "Cluster_47_Sector001_macro" }  // Trinity Sanctum VII
    { Territory.Default with faction = "holyorder"; sector = "Cluster_24_Sector001_macro" }  // Holy Vision
    { Territory.Default with faction = "holyorder"; sector = "Cluster_11_Sector001_macro" }  // Pontifex Claim

    // split: zyarth. freesplit: free families
    { Territory.Default with faction = "split"; sector = "Cluster_418_Sector001_macro" }      // Family Nhuut
    { Territory.Default with faction = "split"; sector = "Cluster_401_Sector001_macro" }      // Family Zhin
    { Territory.Default with faction = "freesplit"; sector = "Cluster_411_Sector001_macro" }  // Heart of Acrmony II
    { Territory.Default with faction = "freesplit"; sector = "Cluster_410_Sector001_macro" }  // Tharkas Ravine XVI
    { Territory.Default with faction = "freesplit"; sector = "Cluster_412_Sector001_macro" }  // Tharkas Ravine VIII

    // cradle of humanity
    { Territory.Default with faction = "terran"; sector = "Cluster_101_Sector001_macro" }     // Mars
    { Territory.Default with faction = "terran"; sector = "Cluster_100_Sector001_macro" }     // Asteroid belt
    { Territory.Default with faction = "pioneers"; sector = "Cluster_113_Sector001_macro" }   // Segaris sectors are Cluster_113_Sector001_macro -> 115  - but we'll leave them unchanged.
    { Territory.Default with faction = "pioneers"; sector = "Cluster_114_Sector001_macro" }   // Segaris sectors are Cluster_113_Sector001_macro -> 115  - but we'll leave them unchanged.
    { Territory.Default with faction = "pioneers"; sector = "Cluster_115_Sector001_macro" }   // Segaris sectors are Cluster_113_Sector001_macro -> 115  - but we'll leave them unchanged.

    // tides of avarice
    // :eave VIG/Scavengers mostly unchanged. Leave Windfall I for sure to avoid issues with Erlking.
    { Territory.Default with faction = "scavenger"; cluster = "cluster_500" }   // RIP: Unchanged. All sectors in cluster_500
    { Territory.Default with faction = "loanshark"; cluster = "cluster_501" }   // Leave VIG unchanged.
    { Territory.Default with faction = "loanshark"; cluster = "cluster_502" }   // 
    { Territory.Default with faction = "loanshark"; cluster = "cluster_503" }   // I considered removing this cluster, but it has the scrap VIG need, so will leave it.

    // boron
    // Boron: Economy is kinda screwed without player help anyway. Leave them alone for now?
    // Changing it could screw things with the default boron story if there are Xenon swarming around.
    // { Territory.Default with faction = "boron"; cluster = "cluster_601" }       // Watchful Gaze Not in territory by default.
    { Territory.Default with faction = "boron"; cluster = "cluster_602" }       // Barren Shores 
    { Territory.Default with faction = "boron"; cluster = "cluster_603" }       // Great Reef 
    { Territory.Default with faction = "boron"; cluster = "cluster_604" }       // Ocean of Fantasy
    // { Territory.Default with faction = "boron"; cluster = "cluster_605" }       // Sanctuary of Darkness : The Khaak sector
    { Territory.Default with faction = "boron"; cluster = "cluster_606" }       // Kingdom End (cluster with 3 sectors) : Kingdoms end I, Reflected Stars, Towering Waves 
    { Territory.Default with faction = "boron"; cluster = "cluster_607" }       // Rolk's Demise 
    { Territory.Default with faction = "boron"; cluster = "cluster_608" }       // Atreus' Clouds
    { Territory.Default with faction = "boron"; cluster = "cluster_609" }       // Menelaus' Oasis 
]


// Seems that most locations withing a cluster have the string 'cluster_xxx' in their name somewhere
// Lets look for that.
let getClusterFromLocation (location:string) =
    // split the location string in to words separated by '_', and convert to lowercase 
    let words = location.ToLower().Split('_') |> Array.toList

    let rec loop (words: string list) =
        match words with
        | [] -> None            // Empty list
        | _last :: [] -> None    // If theres only one word left, it can't be a cluster followed by ID
        | "cluster" :: id :: _tail -> Some $"cluster_{id}" 
        | _head :: tail -> loop tail
    loop words



// TODO: All these location sector/cluster functions need to be reconsidered as we
// change how we're handling sectors and clusters for the mod


let isLocationInCluster (location:string) (cluster:string) =
    let locationCluster = getClusterFromLocation location|> Option.defaultValue "none"
    let clusterName = getClusterFromLocation cluster |> Option.defaultValue "none"
    locationCluster =? clusterName

// Explicit check for whether we've defined a cluster in the mapping. For many factions, this will
// return 'false' even if they do have presence in sectors in the cluster. For the more general case
// you'll probably want to use 'doesFactionHavePresenceInLocationCluster' instead.
let isFactionInCluster (faction: string) (cluster: string) =
    let clusterName = getClusterFromLocation cluster |> Option.defaultValue "none"  // First step is that 'cluster' is often recorded as 'cluster_[id]_macro' - so extract the actual name
    // First check is simply whether we have a cluster defined and it matches
    territories |> List.exists (fun record -> record.faction = faction && record.cluster =? clusterName) 


let findRecordsByFaction (faction: string) records =
    records |> List.filter (fun record -> record.faction = faction)


// This function returns whether a faction is ALLOWED to be in the sector as per our mod rules
// For most factions this is a lot less than what is in the base game.
// Normally the territories explitly list the sectors, but for some factions, like VIG, we
// set sector to '', and List clusters instead. We'll have to check both.
let isFactionInSector (faction: string) (sector: string) =
    // The the given sector straigh up listed in the territories list? Otherwsie, check clusters
    territories |> List.exists (fun record -> record.faction = faction && record.sector =? sector)
    ||  isFactionInCluster faction sector




// 'true' if the faction has any sort of presence in the cluster, even if it's just one sector.
// Used for certain jobs, but not appropriate for stations, as those are assigned to a specific sector.
let doesFactionHavePresenceInLocationCluster (faction: string) (location: string) =
    isFactionInCluster faction location
    || isFactionInSector faction location
    || territories |> List.exists (
        // Otherwise, lets try extract teh cluster from the sector for an approximate match.
        fun record ->
            let recordCluster = getClusterFromLocation record.sector |> Option.defaultValue "none"
            record.faction = faction && isLocationInCluster record.sector location 
    )


// Using the data in sector.xml, which is represented by the X4Sector type, find the name of
// the sector given the name of the zone. the zone is stored as a connection in the sector definition.
let findSectorFromZone (zone:string) (sectors:X4Sector.Macro list) =
    // allSectors is a list of secto Macros. Each macro represents a sector. In that sector we'll find connections.
    // Each connection will have zero or more zones for use to check. So we try find a macro that contains a zone with the name we're looking for.
    // Then return the name of that macro.
    allSectors |> List.tryFind (
        fun sector -> 
            sector.Connections |> Array.tryFind (
                fun connection -> connection.Ref = "zones" && connection.Macro.Connection = "sector" && connection.Macro.Ref =? zone
            ) |> Option.isSome 
    )
    |> Option.map (fun sector -> sector.Name.ToLower()) // return the sector name, but in lower case, as the case varies in the files. I prefer to make it consistent


// Given a cluster name, return the X4Cluster object representing it.
let findCluster (clusterName:string) =
    AllClusters |> List.tryFind (fun cluster -> cluster.Name =? clusterName)


// Given a cluster name, find it, and then return all of it's connections of the specific type in a list.
// Note that here, unlike in other places, the type is pluralised. eg, don't search for 'sector', use 'sectors'
// Returns empty list if no sectors found.
let getClusterConnectionsByType clusterName connectionType =
    findCluster clusterName
    |>  Option.map (
            fun cluster -> 
                cluster.Connections
                |> Array.toList 
                |> List.filter (fun connection -> connection.Ref = connectionType
        )
    )
    |> Option.defaultValue []


// Given a cluster name, return all the X4Sector objects in a list.
let findSectorsInCluster (cluster:string) =
    getClusterConnectionsByType cluster "sectors"
    |> List.map (fun connection -> Option.defaultValue "no_sector_name" connection.Macro.Ref)
    |> List.map (fun sector -> sector.ToLower()) // Lower case for consistency


// Given a sector name, which cluster does it belong to?
let findClusterFromSector (sector:string) =
    getClusterFromLocation sector // Maybe we can do better by checking the connections explicitly, but Egosoft have reliably named the sectors with the cluster name in them.

let findFactionFromCluster (cluster: string) =
    territories |> List.tryFind (fun record -> record.cluster =? cluster) |> Option.map (fun record -> record.faction)

let findFactionFromSector (sector: string) =
   match territories |> List.tryFind (fun record -> record.sector =? sector) |> Option.map (fun record -> record.faction) with
   | Some faction -> Some faction
   | None ->
        match findClusterFromSector sector with
        | None -> None
        | Some cluster -> findFactionFromCluster cluster

let findFactionFromZone (zone: string) =
    let sector = findSectorFromZone zone allSectors
    match sector with
    | None -> None
    | Some sector -> findFactionFromSector sector

let dump_sectors (sectors:X4Sector.Macro list) =
    for sector in sectors do
        printfn "Macro.%s," (sector.Name.ToLower())

