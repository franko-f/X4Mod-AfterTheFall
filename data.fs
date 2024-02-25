/// <summary>
/// This module contains data around factions, sectors and rules we'll use to
/// generate our new universe.
/// </summary>

module X4.Data

open FSharp.Data
open X4.Utilities

[<Literal>]
let X4UnpackedDataFolder = __SOURCE_DIRECTORY__ + "/X4_unpacked_data"

// The default GOD.xml file, etc, don't have an 'add/replace' XML section, so they can't
// be used as type providers for our output XML. So we've created a template that has
// versions of our types that we need (like station, product) as well as the XML selectors
// for replace/add
[<Literal>]
let X4GodModFile = __SOURCE_DIRECTORY__ + "/mod_templates/god.xml"
[<Literal>]
let X4ObjectTemplatesFile = __SOURCE_DIRECTORY__ + "/mod_templates/object_templates.xml"

[<Literal>]
let X4GodFileCore   = X4UnpackedDataFolder + "/core/libraries/god.xml" // Core game data.
let X4GodFileSplit  = X4UnpackedDataFolder + "/split/libraries/god.xml" // Core game data.
let X4GodFileTerran = X4UnpackedDataFolder + "/terran/libraries/god.xml" // Core game data.
let X4GodFilePirate = X4UnpackedDataFolder + "/pirate/libraries/god.xml" // Core game data.
let X4GodFileBoron  = X4UnpackedDataFolder + "/boron/libraries/god.xml" // Core game data.

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

// TODO: Should we 'unify' all the types using 'Global=true,' parameter?
// This means, for example, every instance of 'Location' type is trested as the same type, no matter
// where it appears in the sample data file. It results in a lot of fields being set to an 'option'
// type, as some will appear in some cases of location, but not in others. So requires some tweaks
// to the parsing code.
// https://fsprojects.github.io/FSharp.Data/library/XmlProvider.html#Global-inference-mode
type X4WorldStart = XmlProvider<X4GodFileCore>
type X4GodMod     = XmlProvider<X4GodModFile >
type X4ObjectTemplates = XmlProvider<X4ObjectTemplatesFile>

type X4Cluster    = XmlProvider<X4ClusterFileCore>
type X4Sector     = XmlProvider<X4SectorFileCore>
type X4Zone       = XmlProvider<X4ZoneFileCore>
type X4Galaxy     = XmlProvider<X4GalaxyFileCore>
// the DLC galaxy files are in DIFF format, so we need a different type provider.
type X4GalaxyDiff = XmlProvider<X4GalaxyFileSplit>


// ====== LOAD DATA FROM XML FILES ======

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


// Read all thge stations and products from the core game and the DLCs.
let allStations, allProducts =
    // Helper functions. Extract the stations from the 'add' section of a DLCs god diff/mod file.
    // While there are many 'add' sections, we're only interested in the one that that has the selectior '//god/stations'
    let getStationsFromDiff (diff:X4GodMod.Add[]) =
        let stationsAdd = Array.filter (fun (add:X4GodMod.Add) -> add.Sel = "/god/stations") diff
        [ for stations in stationsAdd do
                for station in stations.Stations do
                    yield new X4WorldStart.Station(station.XElement)
        ]

    let getProductFromDiff (diff:X4GodMod.Add[]) =
        let productsAdd = Array.filter (fun (add:X4GodMod.Add) -> add.Sel = "/god/products") diff
        [ for products in productsAdd do
                for product in products.Products do
                    yield new X4WorldStart.Product(product.XElement)
        ]

    let X4GodCore   = X4WorldStart.Load(X4GodFileCore)
    let X4GodSplit  = X4GodMod.Load(X4GodFileSplit)
    let X4GodTerran = X4GodMod.Load(X4GodFileTerran)
    let X4GodPirate = X4GodMod.Load(X4GodFilePirate)
    let X4GodBoron  = X4GodMod.Load(X4GodFileBoron)

    // Finally build up an uberlist of all our stations across all DLC and core game.
    // The DLC stations are of a different type: they're an XML DIFF file, not the GOD
    // file type. So we need to pull out the stations from the diff and convert them
    // to the same type as the core stations using the underlying XElement.
    let allStations =
        List.concat [
            Array.toList X4GodCore.Stations.Stations
            getStationsFromDiff X4GodSplit.Adds
            getStationsFromDiff X4GodTerran.Adds
            getStationsFromDiff X4GodPirate.Adds
            getStationsFromDiff X4GodBoron.Adds
        ]

    // Do the same for products
    let allProducts =
        List.concat [
            Array.toList X4GodCore.Products
            getProductFromDiff X4GodSplit.Adds
            getProductFromDiff X4GodTerran.Adds
            getProductFromDiff X4GodPirate.Adds
            getProductFromDiff X4GodBoron.Adds
        ]

    allStations, allProducts


// ===== FINISHED LOADING DATA FROM XML FILES =====

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
type Territory = { faction: string; cluster:string; resources: MiningResource list }
                   static member Default = { faction = ""; cluster = ""; resources = []}

// create a list of all the factions and their territories as Territory records
// We'll pull resources and gates from the xml files, based on the sector name.
// We will likely need to create a zone (or find) in each of these sectors to place the station
let territories = [
    // core
    { Territory.Default with faction = "argon";    cluster = "Cluster_44_macro" }   // Silent Witness XI
    { Territory.Default with faction = "argon";    cluster = "Cluster_45_macro" }   // Silent Witness XII
    { Territory.Default with faction = "hatikvah"; cluster = "Cluster_46_macro" }   // Morning Star IV
    { Territory.Default with faction = "antigone"; cluster = "Cluster_27_macro" }   // The Void
    { Territory.Default with faction = "antigone"; cluster = "Cluster_28_macro" }   // Antigone Menorial

    { Territory.Default with faction = "teladi";   cluster = "Cluster_42_macro" }   // Hewas Twin III, IV, V
    { Territory.Default with faction = "teladi";   cluster = "Cluster_43_macro" }
    { Territory.Default with faction = "ministry"; cluster = "Cluster_32_macro" }
//    { faction = "scaleplate"; sector = "" }

    { Territory.Default with faction = "paranid";   cluster = "Cluster_37_macro" }  // Pious IV
    { Territory.Default with faction = "paranid";   cluster = "Cluster_38_macro" }  // Pious XI
    { Territory.Default with faction = "alliance";  cluster = "Cluster_38_macro" }  // If we have to move an ALI station, move it to PAR space.
    { Territory.Default with faction = "holyorder"; cluster = "Cluster_35_macro" }  // Lasting Vengence
    { Territory.Default with faction = "holyorder"; cluster = "Cluster_36_macro" }  // Cardinals Redress

    // split: zyarth. freesplit: free families
    { Territory.Default with faction = "split";     cluster = "Cluster_405_macro" }  // Zyarth Dominion IV
    { Territory.Default with faction = "split";     cluster = "Cluster_406_macro" }  // Zyarth Dominion X
    { Territory.Default with faction = "freesplit"; cluster = "Cluster_410_macro" }  // Tharkas Ravine XVI
    { Territory.Default with faction = "freesplit"; cluster = "Cluster_411_macro" }  // Heart of Acrmony II
    { Territory.Default with faction = "freesplit"; cluster = "Cluster_412_macro" }  // Tharkas Ravine VIII

    // cradle of humanity
    { Territory.Default with faction = "terran";   cluster = "Cluster_104_macro" }   // Earth and the Moon
    { Territory.Default with faction = "pioneers"; cluster = "Cluster_113_macro" }   // Segaris
    { Territory.Default with faction = "pioneers"; cluster = "Cluster_114_macro" }   // Gaian Prophecy

    // tides of avarice
    // :eave VIG/Scavengers mostly unchanged. Leave Windfall I for sure to avoid issues with Erlking. (Or figure out how to move it in the future.)
    { Territory.Default with faction = "scavenger"; cluster = "Cluster_500_macro" }   // RIP: Unchanged. All sectors in cluster_500
    { Territory.Default with faction = "loanshark"; cluster = "Cluster_501_macro" }   // Leave VIG unchanged.
    { Territory.Default with faction = "loanshark"; cluster = "Cluster_502_macro" }   // 
    //{ Territory.Default with faction = "loanshark"; cluster = "Cluster_503_macro" }

    // boron
    // Boron: Economy is kinda screwed without player help anyway. Leave them a few more sectors than most.
    // Removing These territories may screw up the default storyline, so players will need to set story complete in gamestart.
    // Cluster_602_macro: Barren Shores, Cluster_603_macro: Great Reef, Cluster_604_macro: Ocean of Fantasy
    { Territory.Default with faction = "boron"; cluster = "Cluster_606_macro" }       // Kingdom End (cluster with 3 sectors) : Kingdoms end I, Reflected Stars, Towering Waves 
    { Territory.Default with faction = "boron"; cluster = "Cluster_607_macro" }       // Rolk's Demise 
    { Territory.Default with faction = "boron"; cluster = "Cluster_608_macro" }       // Atreus' Clouds
    { Territory.Default with faction = "boron"; cluster = "Cluster_609_macro" }       // Menelaus' Oasis 
]


// Given a cluster name, return the X4Cluster object representing it.
let findCluster (clusterName:string) =
    AllClusters |> List.tryFind (fun cluster -> cluster.Name =? clusterName)

let getClusterMacroConnectionsByType connectionType (cluster:X4Cluster.Macro)  =
    cluster.Connections
    |> Array.toList
    |> List.filter (fun connection -> connection.Ref = connectionType )

// Given a cluster name, find it, and then return all of it's connections of the specific type in a list.
// Note that here, unlike in other places, the type is pluralised. eg, don't search for 'sector', use 'sectors'
// Returns empty list if no sectors found.
let getClusterConnectionsByType connectionType clusterName =
    findCluster clusterName
    |> Option.map ( fun cluster -> getClusterMacroConnectionsByType connectionType cluster)
    |> Option.defaultValue []

// Given a cluster name, return all the X4Sector objects in a list.
let findSectorsInCluster (cluster:string) =
    getClusterConnectionsByType "sectors" cluster
    |> List.map (fun connection -> Option.defaultValue "no_sector_name" connection.Macro.Ref)
    |> List.map (fun sector -> sector.ToLower()) // Lower case for consistency

let getFactionClusters (faction: string) =
    territories |> List.filter (fun record -> record.faction = faction) |> List.map (fun record -> record.cluster)

let getFactionSectors (faction: string) =
    getFactionClusters faction |> List.collect findSectorsInCluster

// Given a sector name, which cluster does it belong to?
let findClusterFromSector (sector:string) =
    AllClusters |>
    List.tryFind (
        // For each cluster, we'll check if there's a connection to this sector.
        fun cluster ->
            getClusterMacroConnectionsByType "sectors" cluster
            |> List.exists ( fun c -> Option.defaultValue "no_sector_name" c.Macro.Ref =? sector )
        )
    // If we actually found a match, change the return value from Some Cluster to Some Cluster.Name
    |> Option.map (fun cluster -> cluster.Name)

// Using the data in sector.xml, which is represented by the X4Sector type, find the name of
// the sector given the name of the zone. the zone is stored as a connection in the sector definition.
let findSectorFromZone (zone:string) =
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


// Explicit check for whether we've ALLOWED a faction in a cluster in our territory mapping.
// For most factions this is a lot less than what is in the base game.
let isFactionInCluster (faction: string) (cluster: string) =
    territories |> List.exists (fun record -> record.faction = faction && record.cluster =? cluster)

// This function returns whether a faction is ALLOWED to be in the sector as per our mod rules
let isFactionInSector (faction: string) (sector: string) =
    findClusterFromSector sector |> Option.map (fun cluster -> isFactionInCluster faction cluster) |> Option.defaultValue false

// Have we ALLOWED the faction to be in this specific zone?
let isFactionInZone (faction: string) (zone: string) =
    match findSectorFromZone zone with
    | None -> false
    | Some sector -> isFactionInSector faction sector

// Given any location name and class, return whether the faction is ALLOWED to be in that location.
let isFactionInLocation (faction: string) (location: string) (locationClass:string) =
    match locationClass with
    | "galaxy"  -> true // well, if the class is galaxy, then definitely
    | "sector"  -> isFactionInSector faction location
    | "cluster" -> isFactionInCluster faction location
    | "zone"    -> isFactionInZone faction location
    | _ -> failwith ("Unhandled location class in job: " + locationClass)


let findFactionFromCluster (cluster: string) =
    territories |> List.tryFind (fun record -> record.cluster =? cluster) |> Option.map (fun record -> record.faction)

let findFactionFromSector (sector: string) =
    match findClusterFromSector sector with
    | Some cluster -> findFactionFromCluster cluster
    | None -> None

let findFactionFromZone (zone: string) =
    match findSectorFromZone zone with
    | None -> None
    | Some sector -> findFactionFromSector sector


let dump_sectors (sectors:X4Sector.Macro list) =
    for sector in sectors do
        printfn "Macro.%s," (sector.Name.ToLower())
