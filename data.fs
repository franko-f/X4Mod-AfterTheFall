/// <summary>
/// This module contains data around factions, sectors and rules we'll use to
/// generate our new universe.
/// </summary>

module X4.Data

open Microsoft.FSharp.Core
open FSharp.Data
open X4.Utilities
open System
open System.IO
open System.Xml
open System.Xml.Linq

let rand = new Random(12345)    // Seed the random number generator so we get the same results each time as long as were not changing code.

let ContentDirectories   = ["core"; "split"; "terran"; "pirate"; "boron"; "timelines"]
let ShipSizeClasses      = ["ship_s"; "ship_m"; "ship_l"; "ship_xl"]
let ShipSizeDirectories  = ["size_s"; "size_m"; "size_l"; "size_xl"]  // Oddly, the ship size directory names are not the same as the ship size classes.
let ComponentSizeClasses = ["small"; "medium"; "large"; "extralarge"] // I really wish there was some kind of consistency when it comes to referring to sizes.
let ShipEquipmentClasses = [
    // Allow us to filter down all assets to the ship equipment we're interested in.
    // Ship mounted equipment
    "engine"; "shieldgenerator"; "weapon"; "missileturret"; "missilelauncher"; "turret"; 
    // Ship deployables.
    "missile"; "resource_probe"; "satellite"
]

let ShipEquipmentConnectionTag  = ["weapon"; "turret"; "shield"; "engine"; "thruster" ]

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

// Regions for mining fields
[<Literal>]
let X4RegionDefinitionsFile = X4UnpackedDataFolder + "/core/libraries/region_definitions.xml"

[<Literal>]
let X4RegionYieldsFile = X4UnpackedDataFolder + "/core/libraries/regionyields.xml"

// Ships
[<Literal>]
let X4IndexMacrosFile = X4UnpackedDataFolder + "/core/index/macros.xml"
let X4IndexMacrosFileSplit = X4UnpackedDataFolder + "/split/index/macros.xml"
let X4IndexMacrosFileTerran = X4UnpackedDataFolder + "/terran/index/macros.xml"
let X4IndexMacrosFilePirate = X4UnpackedDataFolder + "/pirate/index/macros.xml"
let X4IndexMacrosFileBoron = X4UnpackedDataFolder + "/boron/index/macros.xml"


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
type X4GalaxyDiff = XmlProvider<X4GalaxyFileSplit> // the DLC galaxy files are in DIFF format, so we need a different type provider.

type X4RegionDefinitions = XmlProvider<X4RegionDefinitionsFile>
type X4RegionYields = XmlProvider<X4RegionYieldsFile>


// Ships and loadouts
[<Literal>]
let X4UnitsDirectory = "/assets/units"
[<Literal>]
// Use the Argon detroyer as an XMLProvider template for loading units
let X4UnitsXMLProviderTemplateFile = X4UnpackedDataFolder + "/core" + X4UnitsDirectory + "/size_l/ship_arg_l_destroyer_01.xml"

type X4IndexMacro = XmlProvider<X4IndexMacrosFile>
type X4Ships      = XmlProvider<X4UnitsXMLProviderTemplateFile> // in the 'units' assets directory, but we only care about the ships.

type ShipInfo = {
    Name: String
    Size: String
    Connections: X4Ships.Connection array
}


// Equipment : weapons, shields, etc
[<Literal>]
let X4EquipmentDirectory = "/assets/props"
[<Literal>]
let X4EquipmentXMLProviderTemplateFile = X4UnpackedDataFolder + "/core" + X4EquipmentDirectory + "/WeaponSystems/capital/weapon_arg_l_destroyer_01_mk1.xml"

type X4Equipment  = XmlProvider<X4EquipmentXMLProviderTemplateFile>
type EquipmentInfo = {
    Name: String
    Macro: String  // Same as name with _macro suffix
    Class: String
    Size: String
    Tags: String List
    ComponentName: String
    ComponentConnection: X4Equipment.Connection
    Connections: X4Equipment.Connection array
}


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

let AllIndexMacros =
    let X4IndexMacrosCore   = X4IndexMacro.Load(X4IndexMacrosFile)
    let X4IndexMacrosSplit  = X4IndexMacro.Load(X4IndexMacrosFileSplit)
    let X4IndexMacrosTerran = X4IndexMacro.Load(X4IndexMacrosFileTerran)
    let X4IndexMacrosPirate = X4IndexMacro.Load(X4IndexMacrosFilePirate)
    let X4IndexMacrosBoron  = X4IndexMacro.Load(X4IndexMacrosFileBoron)
    Array.concat [
        X4IndexMacrosCore.Entries;
        X4IndexMacrosSplit.Entries;
        X4IndexMacrosTerran.Entries;
        X4IndexMacrosPirate.Entries;
        X4IndexMacrosBoron.Entries;
    ]

let allRegionDefinitions = X4RegionDefinitions.Load(X4RegionDefinitionsFile)
let regionYields = X4RegionYields.Load(X4RegionYieldsFile)

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

// Type that specifies a station type and the class, locationj it will appear in.
// ego: XenonShipyard "sector" "cluster_110_sector001_macro"
type XenonStation =
    | XenonShipyard of string * string
    | XenonWharf of string * string

// add new Xenon shipyards/wharfs to the following clusters:
let newXenonStations = [
    XenonShipyard   ("sector", "Cluster_46_sector001_macro");  // Morningstar IV
    XenonWharf      ("sector", "Cluster_46_sector001_macro");

    XenonShipyard   ("sector", "Cluster_100_sector001_macro");   // Asteroid belt
    XenonShipyard   ("sector", "Cluster_109_sector001_macro");   // Uranus

    XenonWharf      ("sector", "Cluster_413_sector001_macro");      // Tharka Ravine IV: Tharkas Fall
]

// ===== FINISHED LOADING DATA FROM XML FILES =====

// Gates are linked to a zone by using one of the following as a reference. So by looking
// for these references in the zone file, we can find the gates in a zone.
// I don't think we actually need this. More investigation seems to suggest that a gate is
// identified by a zone connection ref="gates" instead. If correct, we can remove this.
let gateMacros = ["props_gates_orb_accelerator_01_macro", "props_gates_anc_gate_macro", "props_ter_gate_01_macro"]


// lookup map to the resource definitions from the XML that we will use to place extra resources
// for factions now in sectors without resources.
let resourceMap = Map [
    "minerals", "atf_60km_asteroid_field_high";     // ore, silicon and a little bit of nvidium
    "ice",      "atf_60km_ice_field_high";          // ice
    "scrap",    "atf_wreckfield_xenon_battle_30km"; // scrap
    "hydrogen", "p1_40km_hydrogen_field";
    "helium",   "p1_40km_helium_highyield_field";
    "methane",  "p1_40km_methane_highyield_field"
]
// The standard resources that we'll use to populate the sectors. Two halves, one for each system.
let standardResources1stHalf = ["hydrogen"; "helium"; "methane" ]
let standardResources2ndHalf = [ "minerals"; "minerals"; "ice"; "scrap"; ] // 2x minerals to make it more common

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

// In addition to the games default collection of sectors/clusters that have no owners, we're going
// to add a few more, so that players have options on almost-safe sectors in which to start their own
// empire should they wish it
let neutralClusters = [
    "Cluster_01_macro"      // Grand Exchange, 3 sectors
    "Cluster_27_macro"      // Eighteen Billion
    // "Cluster_46_macro"      // Morningstar IV  - Changed to Xenon now that we've moved ARG to Morningstar III
    "Cluster_401_macro"     // Family Zhin
    "Cluster_422_macro"     // Wretched Skies X
    "Cluster_116_macro"     // Oort Cloud
    "Cluster_13_macro"      // second contact - Give ARG/ANt some breathing room, just a little, before Xenon storm through.
    "Cluster_09_macro"      // Bright promise - again, sector between TEL and PAR, to give a little bit of time before xenon play havoc
]

type Territory = { faction: string; cluster:string; resources: string list }
                   static member Default = { faction = ""; cluster = ""; resources = []}

// create a list of all the factions and their territories as Territory records
// We'll pull resources and gates from the xml files, based on the sector name.
// We will likely need to create a zone (or find) in each of these sectors to place the station
let territories = [
    // core
    { Territory.Default with faction = "argon";    cluster = "Cluster_07_macro"; resources=standardResources1stHalf }   // The Reach
    { Territory.Default with faction = "argon";    cluster = "Cluster_14_macro"; resources=standardResources2ndHalf }   // Argon Prime
    { Territory.Default with faction = "argon";    cluster = "Cluster_30_macro"; resources=List.concat([standardResources1stHalf; standardResources2ndHalf]) } // Morningstar III - Lets add more resources here where it's more exposed and dangerous.

    { Territory.Default with faction = "hatikvah"; cluster = "Cluster_29_macro"; resources=standardResources1stHalf }     // Hatikvahs Choice . ARG also have a station in one of the sectors in Hat choice via manual station assignment.

    { Territory.Default with faction = "antigone"; cluster = "Cluster_27_macro"; resources=["ice"; "scrap"] }  // The Void
    { Territory.Default with faction = "antigone"; cluster = "Cluster_28_macro"; resources=["minerals"] }      // Antigone Memorial
    { Territory.Default with faction = "antigone"; cluster = "Cluster_49_macro"; resources=[] }                // Frontiers Edge.

    { Territory.Default with faction = "teladi";   cluster = "Cluster_15_macro"; resources=List.concat([standardResources1stHalf; standardResources2ndHalf; ["minerals"; "scrap"; "methane"; "hydrogen"]]) }   // Ianumas Zura
    { Territory.Default with faction = "teladi";   cluster = "Cluster_408_macro"; resources=[] }   // Thuruks Demise: A sector from Split DLC. Leaves Freelsplit just slightly less isolated. Won't put resources here due to a flaw in our code. Needs a refactor to permit added resources to another factions DLC sector.
    { Territory.Default with faction = "ministry"; cluster = "Cluster_15_macro" }   // No need for resources, they're in teladi sectors already.
//    { faction = "scaleplate"; sector = "" }

    { Territory.Default with faction = "paranid";   cluster = "Cluster_18_macro"; resources=["minerals"] }    // Trinity III - already has some resources, but adding more.
    { Territory.Default with faction = "paranid";   cluster = "Cluster_47_macro"; resources=["scrap"] }      // Trinity VII
    { Territory.Default with faction = "paranid";   cluster = "Cluster_10_macro"; resources=List.concat([standardResources1stHalf; standardResources2ndHalf])  }      // Unholy Retribution
    { Territory.Default with faction = "alliance";  cluster = "Cluster_47_macro" }  // If we have to move an ALI station, move it to PAR space.

    // 7.0 added new sectors which made the HOP placement a lot easier than before.
    { Territory.Default with faction = "holyorder"; cluster = "Cluster_35_macro"; resources=["helium"; "methane"; "ice"; "minerals"; "minerals"] }  // Lasting Vengence
    { Territory.Default with faction = "holyorder"; cluster = "Cluster_36_macro"; resources=["minerals"; "scrap"; "helium"; "methane"] }  // Cardinals Redress =
    { Territory.Default with faction = "holyorder"; cluster = "Cluster_714_macro"; resources=["minerals"; "methane"; "helium"] }          // 7.0 introduces the perfectly placed 'Freedoms Reach' just under cardinals redress. We'll add a few resources though.

    // split: zyarth. freesplit: free families
    { Territory.Default with faction = "split";     cluster = "Cluster_405_macro"; resources=List.concat([["minerals"; "minerals"; "methane"; "hydrogen"]; standardResources1stHalf]) }  // Zyarth Dominion IV. These sectors are completely without resources, so through on some extra for ZYA
    { Territory.Default with faction = "split";     cluster = "Cluster_406_macro"; resources=List.concat([["minerals"; "helium"; "hydrogen"]; standardResources2ndHalf]) }  // Zyarth Dominion X
    { Territory.Default with faction = "split";     cluster = "Cluster_417_macro"; resources=List.concat([standardResources1stHalf; standardResources2ndHalf])}  // 11th Hour: Former Argon, but added in split DLC.

    { Territory.Default with faction = "freesplit"; cluster = "Cluster_410_macro"; resources=["minerals"; "scrap"; "methane"; "helium"] }      // Tharkas Ravine XVI
    { Territory.Default with faction = "freesplit"; cluster = "Cluster_411_macro"; resources=["minerals"; "helium"; "methane"; "hydrogen"] }                // Heart of Acrmony II
    { Territory.Default with faction = "freesplit"; cluster = "Cluster_412_macro"; resources=["minerals"] }  // Tharkas Ravine VIII

    // cradle of humanity
    { Territory.Default with faction = "terran";   cluster = "Cluster_104_macro"; resources= List.concat([standardResources1stHalf; standardResources2ndHalf]) }   // Earth and the Moon
    { Territory.Default with faction = "terran";   cluster = "Cluster_102_macro"; resources= List.concat([standardResources1stHalf; standardResources2ndHalf; ["helium"; "hydrogen"; "methane"; "minerals"]]) }   // venus
    { Territory.Default with faction = "pioneers"; cluster = "Cluster_113_macro" }   // Segaris   - Plenty resources already, and next door to ANT.
    { Territory.Default with faction = "pioneers"; cluster = "Cluster_114_macro" }   // Gaian Prophecy
    { Territory.Default with faction = "pioneers"; cluster = "Cluster_115_macro" }   // Brennans Triumph. Since pioneers never seem to take territory, we'll leave them with their full original range.

    // tides of avarice
    // :eave VIG/Scavengers mostly unchanged. Leave Windfall I for sure to avoid issues with Erlking. (Or figure out how to move it in the future.)
    { Territory.Default with faction = "scavenger"; cluster = "Cluster_500_macro" }   // RIP: Unchanged. All sectors in cluster_500
    { Territory.Default with faction = "loanshark"; cluster = "Cluster_501_macro" }   // Leave VIG unchanged.
    { Territory.Default with faction = "loanshark"; cluster = "Cluster_502_macro"; resources=["scrap"] }   //
    { Territory.Default with faction = "loanshark"; cluster = "Cluster_503_macro" }  // Windfall IV.

    // boron
    // Boron: Economy is kinda screwed without player help anyway. Leave them a few more sectors than most.
    // Removing These territories may screw up the default storyline, so players will need to set story complete in gamestart.
    // Cluster_602_macro: Barren Shores, Cluster_603_macro: Great Reef, Cluster_604_macro: Ocean of Fantasy
    { Territory.Default with faction = "boron"; cluster = "Cluster_606_macro"; resources=standardResources1stHalf }       // Kingdom End (cluster with 3 sectors) : Kingdoms end I, Reflected Stars, Towering Waves
    { Territory.Default with faction = "boron"; cluster = "Cluster_607_macro" }       // Rolk's Demise
    { Territory.Default with faction = "boron"; cluster = "Cluster_608_macro"; resources=standardResources2ndHalf }       // Atreus' Clouds
    { Territory.Default with faction = "boron"; cluster = "Cluster_609_macro" }       // Menelaus' Oasis
]

// Get all the factions defined in the speficied DLC
let dlcFactions dlc = X4.WriteModfiles.dlcFactions dlc
// Filter the territories list to only include those that are in the specified DLC
let dlcTerritories dlc =
    let factions = dlcFactions dlc
    territories |> List.filter (fun t -> List.contains t.faction factions)

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

let findClusterFromLocation (locationClass:string) (locationMacro:string) =
    match locationClass with
    | "zone" -> findSectorFromZone locationMacro |> Option.map findClusterFromSector |> Option.flatten
    | "sector" -> findClusterFromSector locationMacro
    | "cluster" -> Some locationMacro
    | _ -> None

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
    | "sector"  -> isFactionInSector  faction location
    | "cluster" -> isFactionInCluster faction location
    | "zone"    -> isFactionInZone    faction location
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


// Get the X, Y, Z position of a cluster, offset from galactic center.
let getClusterPosition (clusterName: string) =
    // Cluster positions are stored as a connection in the galaxy file, not the cluster file.
    allGalaxy
        |> List.tryFind (fun connection -> connection.Ref = "clusters" && connection.Macro.Ref =?? clusterName)
        // Now that we've found the connection, we can get the position from it.
        // This will raise an exception if there's no offset. We want it to fail if the schema has changed.
        |> Option.map (fun connection -> connection.Offset.Value.Position.X, connection.Offset.Value.Position.Y, connection.Offset.Value.Position.Z)
        |> Option.get

// this function wil take an XElement, and return the integer version of the value.
// It will handle both decimals and floating point strings in scientific notation.
// We need this because the Boron DLC, for some reason, has positions in scientific notation.
// eg, 1.234e+005
let getIntValue (element:string) = int(float element)
let parsePosition (element:XElement) =
    let x = getIntValue (element.Attribute("x").Value)
    let y = getIntValue (element.Attribute("y").Value)
    let z = getIntValue (element.Attribute("z").Value)
    x, y, z

// Get the X, Y, Z position of a sector, offset from the galactic center.
// sector position *may* be defined in the cluster.xml connection. If it's not, I'm assuming it defaults
// to the clusters position.
let getSectorPosition (sectorName: string) =
    // Search all clusters for the connection to the sector. We can use this to get the position,
    // either in the connection, or by looking up the cluster position.
    // Start by finding the cluster object that contains this sector.
    let cluster =
        findClusterFromSector sectorName
        |> Option.defaultValue "no_cluster"
        |> findCluster
        |> Option.get   // Crap out if we can't find the cluster. This should never happen, and if it does, it means data has changed.

    // Now look for the connection to the sector in the cluster, and get the position for that connection.
    cluster.Connections
    |> Array.tryFind (fun connection -> connection.Ref = "sectors" && connection.Macro.Ref =?? sectorName)
    |> Option.map (
        fun connection ->
            connection.Offset
            |> Option.map ( fun offset -> parsePosition offset.Position.XElement )
    )
    |> Option.flatten
    // The connection for the sector may not have had a position, in which case it defaults to cluster center: ie, 0,0,0
    |> Option.defaultValue (0, 0, 0)


// Get all the safe sectors in the game. that is, sectors where factions exist, rather than Xenon or neutral
let getSafeSectors =
    allSectors |> List.filter (
        // Filter to every sector that is in a cluster mentioned in the territories list. Those are our safe clusters.
        fun sector ->
            let cluster = findClusterFromSector sector.Name
            territories |> List.exists (fun record -> cluster =?? record.cluster)
    )

// Get all the UNSAFE sectors in the game. That's the sectors that are Xenon in our mod, or neutral.
let getUnsafeSectors = allSectors |> List.except getSafeSectors

let selectRandomSector() = allSectors.[rand.Next(allSectors.Length)]
let selectRandomSafeSector() = getSafeSectors.[rand.Next(getSafeSectors.Length)]
let selectRandomUnsafeSector() = getUnsafeSectors.[rand.Next(getUnsafeSectors.Length)]

let allShipMacros =
    [for entry in AllIndexMacros do
        if entry.Name.StartsWith "ship_" then entry.Name
    ]



// Here we're pulling the actual ship data definition for use in generating loadouts.
// This will deprecate the previous allShipMacros
let allShips =
    // We have to get the ships for each DLC, in a different root dir.
    // Define some helper functions for this.
    let getDLCShips (dlcDir:String) =
        let files = ShipSizeDirectories
                  |> List.map ( fun size -> X4UnpackedDataFolder + "/" + dlcDir + X4UnitsDirectory + "/" + size )
                  |> List.toArray
                  |> Array.collect (fun dir -> Directory.GetFiles(dir, "*.xml") )

        files
            |> Array.filter (fun filename -> not (filename.EndsWith("_macro.xml")))  // Some DLCs save the macro file in this dir too.
            |> Array.choose ( fun file ->
            try
                let parsed = X4Ships.Load(file)
                let name   = parsed.Component.Name
                let macro  = parsed.Component.Class
                let connections = parsed.Component.Connections
                printfn "Loaded ship: %-35s %-7s from %s" name macro file
                let todo_filter_connections_for_equipment_types_and_tags = true
                Some {Name = name; Size = macro; Connections = connections}
            with
            | ex ->
                printfn $"Error loading ship:  {file}: {ex.Message}"
                None
        ) |> Array.toList

    // Ignore timelines ships, as they're mostly one off uniques and mission specific.
    ContentDirectories
        |> List.filter ( (<>) "timelines" )
        |> List.collect getDLCShips
        |> List.filter( fun ship -> ShipSizeClasses |> List.contains ship.Size ) // There are some things defined as ships, that aren't really ships. Ignore them by checking the size class.


let findShipByName (shipName:string) =
    // Find a ship by its name, case insensitive.
    allShips |> List.tryFind (fun ship -> ship.Name =? shipName)

let printShipInfo (ship:ShipInfo) =
    // Print the ship info in a nice format.
    printfn "\n Ship: %s" ship.Name
    ship.Connections
    |> Seq.iter (fun connection -> printfn "  Connection %-50s/%-20s / %s" connection.Name (Option.defaultValue "" connection.Group ) connection.Tags) 

// Each DLC is in a separate directory; and the different types of files describing ships
// and equipment are in a different set of subdirs off of that base of subtype.
// Quick helper function with some common code to pull in all the files from these
// subdirs from each DLC and merge in.
let getDlcXmlFiles dlcs dataDir =
    dlcs
    |> List.map (fun dlc -> X4UnpackedDataFolder + "/" + dlc + dataDir)
    |> List.toArray
    |> Array.collect (
        fun dir ->
            try
                printfn $"Loading XML files from {dir}"
                // Recursively get all XML files in directory and subdirectories
                Directory.GetFiles(dir, "*.xml", SearchOption.AllDirectories)
            with
            | ex ->
                printfn $"Failed to load files from {dir}. Directory may not exist."
                [||]
    )



// Get all the assets defined in the core game and the DLCs. This includes
// equipment for ships, as well as miscellaneous assets like wares, adsigns, etc
let allAssets =
    getDlcXmlFiles ContentDirectories X4EquipmentDirectory
    |> Array.toList
    |> List.filter (fun x -> not (x.EndsWith("_macro.xml"))) // Ignore macro files, as they are not the definition we're looking for.
    |> List.map (fun x ->
        try
            // Now parse the file using the X4Equipment type.
            let parsed = X4Equipment.Load(x)
            // Sometimes the Component.Class may have an extra space at the end, so trim it.
            parsed.Component.XElement.SetAttributeValue("name", parsed.Component.Name.Trim()) // Ensure the name is set correctly in the XElement.
            // printfn "Loaded equipment: %-35s %-20s from %s" parsed.Component.Name parsed.Component.Class x
            [parsed.Component]
        with
        | ex ->
            printfn $"\nError loading equipment: {x}: {ex.Message}"
            // try find root of parse error:
            let raw = System.Xml.Linq.XDocument.Load(x)
            printfn "Children of <components>:"
            raw.Root.Elements() |> Seq.iter (fun e -> printfn "- %s" e.Name.LocalName)
            printfn "End of children.\n"
            []
    )
    |> List.collect ( fun x -> x) // Flatten the array of options to a single list, ignoring None values.

let allAssetsByClass =
    // Group all the assets by their class, so we can easily find them later.
    allAssets
    |> List.groupBy (fun asset -> asset.Class)
    |> Map.ofList // Convert to a map for easy lookup

let allAssetClasses =
    // Get all the unique classes of assets, sorted alphabetically.
    allAssets
    |> List.distinctBy (fun asset -> asset.Class)
    |> List.map (fun asset -> asset.Class)
    |> List.sort

// Get all the assets, and filter them down to only the classes that are ship
// equipment we need to generation ship loadouts.
// Convert the xmln asset to a simplified ShipEquipment type, which gives us easy access
//  to the name, macro, class, tags, size and component connection.
let allShipEquipment =
    // Find out all the different unique classes of assests
    allAssets
    |> List.filter (fun asset -> ShipEquipmentClasses |> List.contains asset.Class)
    |> List.map (fun asset ->
        option {
            let! componentConnection =
                asset.Connections
                |> Array.tryFind (fun connection ->
                    let tags = tagStringToList connection.Tags
                    tags |> List.exists (fun tag -> tag =? "component")
                )

            let tags = tagStringToList componentConnection.Tags |> List.filter (fun tag -> tag <> "component") // Remove the 'component' tag, as it's not needed in the final result.
            return {
                Name  = asset.Name
                Macro = asset.Name + "_macro"
                Class = asset.Class
                Tags  = tags
                Size  =
                    tags
                    |> List.tryFind (
                        fun tag -> ComponentSizeClasses |> List.exists (fun t -> t =? tag)
                    )
                    |> Option.defaultValue "none" // Default to size_s if not found
                ComponentName = componentConnection.Name
                ComponentConnection = componentConnection
                Connections = asset.Connections
            }
        }
    )
    |> List.choose id


// Tags in X4 are given as a single string with a list of space separated words.
// If all the tahs in searchTags are present in targetTags, then we have a match,
// regardless of order, and even if target?s has more tags than searchTags.
let compareTagStrings (searchTags:String) (targetTags:String) =
    // Split the tags by space, and then check if all searchTags are in targetTags.
    let searchTagList = tagStringToList searchTags
    let targetTagList = tagStringToList targetTags
    searchTagList |> List.forall (fun tag -> targetTagList |> List.exists (fun t -> t =? tag))

let compareTags (searchTags:list<String>) (targetTags:list<String>) =
    // Split the tags by space, and then check if all searchTags are in targetTags.
    searchTags |> List.forall (fun tag -> targetTags |> List.exists (fun t -> t =? tag))


// Find an asset by its class and tags. The tags are a space separated string of tags.
// Every one of the tags in searchTags must be present in the asset's tags for a match.
// An asset has multiple connections, each with a set of tags. We just need a match in one of them.
let findMatchingAsset (assetClass:string) searchTags (assets:list<EquipmentInfo>) =
    let TODO_how_do_we_make__sure_we_dont_use_mining_assets_on_combat_ships = true
    // Find an asset by its name, case insensitive.
    assets 
        |> List.filter (fun asset -> asset.Class =? assetClass)
        // Filter down to only those that match the tags.
        |> List.filter (fun asset ->
            // Check if any of the connections have the tags we're looking for.
            compareTags searchTags asset.Tags
        )

let dumpEquipment(asset: EquipmentInfo) =
    printfn "%45s %-15s %-10s %-20s %A" asset.Name asset.Class asset.Size asset.ComponentName asset.Tags

let dumpAllEquipment() =
    printfn "\nAll Equipment:"
    allShipEquipment
    |> List.iter dumpEquipment

let dumpShips() =
    printfn "All Ship Macros:"
    for ship in allShipMacros do printfn "macro.%s," (ship)

let dump_sectors (sectors:X4Sector.Macro list) =
    for sector in sectors do
        printfn "Macro.%s," (sector.Name.ToLower())

let dumpRegionDefinitions() =
    for region in allRegionDefinitions.Regions do
        printfn "Region.%s," (region.Name.ToLower())

let dumpRegionYields() =
    printfn "Discovered Region Yields:"
    for ware in regionYields.Resources do
        printfn "\nResource.%s:" (ware.Ware.ToLower())
        for ryield in ware.Yields do
            printfn "   %12s: yield: %6M over %6i minutes = %7.2f/h/km^2" ryield.Name ryield.Resourcedensity ryield.Replenishtime ((float(ryield.Resourcedensity) / float(ryield.Replenishtime) ) * 60.0)
