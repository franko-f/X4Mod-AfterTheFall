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

open X4.Territories

let rand = new Random(12345) // Seed the random number generator so we get the same results each time as long as were not changing code.

let ContentDirectories = [
    ""
    "ego_dlc_split"
    "ego_dlc_terran"
    "ego_dlc_pirate"
    "ego_dlc_boron"
    "ego_dlc_timelines"
]

[<Literal>]
let X4UnpackedDataFolder = __SOURCE_DIRECTORY__ + "/X4_unpacked_data"

// The default GOD.xml file, etc, don't have an 'add/replace' XML section, so they can't
// be used as type providers for our output XML. So we've created a template that has
// versions of our types that we need (like station, product) as well as the XML selectors
// for replace/add
[<Literal>]
let X4GodModFile = __SOURCE_DIRECTORY__ + "/mod_templates/god.xml"

[<Literal>]
let X4ObjectTemplatesFile =
    __SOURCE_DIRECTORY__ + "/mod_templates/object_templates.xml"

[<Literal>]
let X4GodFileCore = X4UnpackedDataFolder + "/libraries/god.xml" // Core game data.

let X4GodFileSplit =
    X4UnpackedDataFolder + "/extensions/ego_dlc_split/libraries/god.xml" // Core game data.

let X4GodFileTerran =
    X4UnpackedDataFolder + "/extensions/ego_dlc_terran/libraries/god.xml" // Core game data.

let X4GodFilePirate =
    X4UnpackedDataFolder + "/extensions/ego_dlc_pirate/libraries/god.xml" // Core game data.

let X4GodFileBoron =
    X4UnpackedDataFolder + "/extensions/ego_dlc_boron/libraries/god.xml" // Core game data.

[<Literal>]
let X4ClusterFileCore = X4UnpackedDataFolder + "/maps/xu_ep2_universe/clusters.xml"

let X4ClusterFileSplit =
    X4UnpackedDataFolder
    + "/extensions/ego_dlc_split/maps/xu_ep2_universe/dlc4_clusters.xml"

let X4ClusterFileTerran =
    X4UnpackedDataFolder
    + "/extensions/ego_dlc_terran/maps/xu_ep2_universe/dlc_terran_clusters.xml"

let X4ClusterFilePirate =
    X4UnpackedDataFolder
    + "/extensions/ego_dlc_pirate/maps/xu_ep2_universe/dlc_pirate_clusters.xml"

let X4ClusterFileBoron =
    X4UnpackedDataFolder
    + "/extensions/ego_dlc_boron/maps/xu_ep2_universe/dlc_boron_clusters.xml"

[<Literal>]
let X4SectorFileCore = X4UnpackedDataFolder + "/maps/xu_ep2_universe/sectors.xml" // This core sectors file needs to be a literal, as it's also our type provider

let X4SectorFileSplit =
    X4UnpackedDataFolder
    + "/extensions/ego_dlc_split/maps/xu_ep2_universe/dlc4_sectors.xml" // This one is normal string, as we can load and parse using X4SectorCore literal

let X4SectorFileTerran =
    X4UnpackedDataFolder
    + "/extensions/ego_dlc_terran/maps/xu_ep2_universe/dlc_terran_sectors.xml"

let X4SectorFilePirate =
    X4UnpackedDataFolder
    + "/extensions/ego_dlc_pirate/maps/xu_ep2_universe/dlc_pirate_sectors.xml"

let X4SectorFileBoron =
    X4UnpackedDataFolder
    + "/extensions/ego_dlc_boron/maps/xu_ep2_universe/dlc_boron_sectors.xml"

[<Literal>]
let X4ZoneFileCore = X4UnpackedDataFolder + "/maps/xu_ep2_universe/zones.xml"

let X4ZoneFileSplit =
    X4UnpackedDataFolder
    + "/extensions/ego_dlc_split/maps/xu_ep2_universe/dlc4_zones.xml"

let X4ZoneFileTerran =
    X4UnpackedDataFolder
    + "/extensions/ego_dlc_terran/maps/xu_ep2_universe/dlc_terran_zones.xml"

let X4ZoneFilePirate =
    X4UnpackedDataFolder
    + "/extensions/ego_dlc_pirate/maps/xu_ep2_universe/dlc_pirate_zones.xml"

let X4ZoneFileBoron =
    X4UnpackedDataFolder
    + "/extensions/ego_dlc_boron/maps/xu_ep2_universe/dlc_boron_zones.xml"

[<Literal>]
let X4GalaxyFileCore = X4UnpackedDataFolder + "/maps/xu_ep2_universe/galaxy.xml"

[<Literal>] // the DLC galaxy files are in DIFF format, so we need a different type provider.
let X4GalaxyFileSplit =
    X4UnpackedDataFolder
    + "/extensions/ego_dlc_split/maps/xu_ep2_universe/galaxy.xml"

let X4GalaxyFileTerran =
    X4UnpackedDataFolder
    + "/extensions/ego_dlc_terran/maps/xu_ep2_universe/galaxy.xml"

let X4GalaxyFilePirate =
    X4UnpackedDataFolder
    + "/extensions/ego_dlc_pirate/maps/xu_ep2_universe/galaxy.xml"

let X4GalaxyFileBoron =
    X4UnpackedDataFolder
    + "/extensions/ego_dlc_boron/maps/xu_ep2_universe/galaxy.xml"

// Regions for mining fields
[<Literal>]
let X4RegionDefinitionsFile =
    X4UnpackedDataFolder + "/libraries/region_definitions.xml"

[<Literal>]
let X4RegionYieldsFile = X4UnpackedDataFolder + "/libraries/regionyields.xml"

// Ships
[<Literal>]
let X4IndexMacrosFile = X4UnpackedDataFolder + "/index/macros.xml"


// TODO: Should we 'unify' all the types using 'Global=true,' parameter?
// This means, for example, every instance of 'Location' type is trested as the same type, no matter
// where it appears in the sample data file. It results in a lot of fields being set to an 'option'
// type, as some will appear in some cases of location, but not in others. So requires some tweaks
// to the parsing code.
// https://fsprojects.github.io/FSharp.Data/library/XmlProvider.html#Global-inference-mode
type X4WorldStart = XmlProvider<X4GodFileCore>
type X4GodMod = XmlProvider<X4GodModFile>
type X4ObjectTemplates = XmlProvider<X4ObjectTemplatesFile>

type X4Cluster = XmlProvider<X4ClusterFileCore>
type X4Sector = XmlProvider<X4SectorFileCore>
type X4Zone = XmlProvider<X4ZoneFileCore>
type X4Galaxy = XmlProvider<X4GalaxyFileCore>
type X4GalaxyDiff = XmlProvider<X4GalaxyFileSplit> // the DLC galaxy files are in DIFF format, so we need a different type provider.

type X4RegionDefinitions = XmlProvider<X4RegionDefinitionsFile>
type X4RegionYields = XmlProvider<X4RegionYieldsFile>


// Ships and loadouts
[<Literal>]
// Use the Argon detroyer as an XMLProvider template for loading units
let X4ShipsXMLProviderTemplateFile =
    X4UnpackedDataFolder + "/assets/units/size_l/ship_arg_l_destroyer_01.xml"

[<Literal>]
let X4ShipMacroXMLProviderTemplateFile =
    X4UnpackedDataFolder
    + "/assets/units/size_l/macros/ship_arg_l_destroyer_01_b_macro.xml"

type X4IndexMacro = XmlProvider<X4IndexMacrosFile>
type X4Ships = XmlProvider<X4ShipsXMLProviderTemplateFile> // in the 'units' assets directory, but we only care about the ships.
type X4ShipsMacro = XmlProvider<X4ShipMacroXMLProviderTemplateFile> // in the 'units' assets directory, but we only care about the ships.


// Equipment : weapons, shields, etc
[<Literal>]
let X4EquipmentDirectory = "/assets/props"

[<Literal>]
let X4EquipmentXMLProviderTemplateFile =
    X4UnpackedDataFolder
    + X4EquipmentDirectory
    + "/WeaponSystems/capital/weapon_arg_l_destroyer_01_mk1.xml"

type X4Equipment = XmlProvider<X4EquipmentXMLProviderTemplateFile>


// Encapsulates information on an  entry in an index file:
// Name of the entity being referred to; the file the entity is defined in, along with the DLC it belongs to.
type Index = {
    Name: String
    File: String
    DLC: String
}

type Asset = {
    Name: String
    File: String
    DLC: String
    Class: String
    Asset: X4Equipment.Component // In the future, this might be a more generic type if we become interested in more assets than just equipment.
}

type EquipmentInfo = {
    Name: String
    MacroName: String // Same as name with _macro suffix
    Class: String
    Size: String
    //    DLC: String
    Tags: String Set
    ComponentName: String
    ComponentConnection: X4Equipment.Connection
    Connections: X4Equipment.Connection array
}

// ====== LOAD DATA FROM XML FILES ======

// Generate a path to a sub directory for either base game of specific DLC
let getDlcDirectory dlc subDir =
    match dlc with
    | "" -> X4UnpackedDataFolder + "/" + subDir // base game files are in the root of the unpacked data folder.
    | dir -> X4UnpackedDataFolder + "/extensions/" + dir + subDir


// Function that given a subdir, will expand out a list of directories, one for each DLC and the core game.
let getDlcDirectories subDir =
    ContentDirectories |> List.map (fun dlc -> getDlcDirectory dlc subDir)


// Load the cluster data from each individual core/expansion cluster XML file. We'll combine them in to one list.
// Convinience functions to search/manipulate these lists are defined below.
let AllClusters =
    let X4ClusterCore = X4Cluster.Load(X4ClusterFileCore)
    let X4ClusterSplit = X4Cluster.Load(X4ClusterFileSplit)
    let X4ClusterTerran = X4Cluster.Load(X4ClusterFileTerran)
    let X4ClusterPirate = X4Cluster.Load(X4ClusterFilePirate)
    let X4ClusterBoron = X4Cluster.Load(X4ClusterFileBoron)

    Array.toList
    <| Array.concat [
        X4ClusterCore.Macros
        X4ClusterSplit.Macros
        X4ClusterTerran.Macros
        X4ClusterPirate.Macros
        X4ClusterBoron.Macros
    ]

// Load the sector data from each individual sector file. We'll combine them in to one list.
let allSectors =
    let X4SectorCore = X4Sector.Load(X4SectorFileCore)
    let X4SectorSplit = X4Sector.Load(X4SectorFileSplit)
    let X4SectorTerran = X4Sector.Load(X4SectorFileTerran)
    let X4SectorPirate = X4Sector.Load(X4SectorFilePirate)
    let X4SectorBoron = X4Sector.Load(X4SectorFileBoron)

    Array.toList
    <| Array.concat [
        X4SectorCore.Macros
        X4SectorSplit.Macros
        X4SectorTerran.Macros
        X4SectorPirate.Macros
        X4SectorBoron.Macros
    ]

let allZones =
    let X4ZoneCore = X4Zone.Load(X4ZoneFileCore)
    let X4ZoneSplit = X4Zone.Load(X4ZoneFileSplit)
    let X4ZoneTerran = X4Zone.Load(X4ZoneFileTerran)
    let X4ZonePirate = X4Zone.Load(X4ZoneFilePirate)
    let X4ZoneBoron = X4Zone.Load(X4ZoneFileBoron)

    Array.toList
    <| Array.concat [
        X4ZoneCore.Macros
        X4ZoneSplit.Macros
        X4ZoneTerran.Macros
        X4ZonePirate.Macros
        X4ZoneBoron.Macros
    ]


let allGalaxy =
    // we're assuming that the galaxy file just contains connections, and that the connection fields/structure
    // is pretty much the same between core and DLCs. Otherwise this casting from one to the other using the
    // XElement is dangerous. This only runs on mod creation though, and if it crashes it means something has
    // changed that we need to account for anyway.
    let loadFromDiff (diff: X4GalaxyDiff.Diff) =
        // Galaxy file just contains a list of connections.
        [|
            for connection in diff.Add.Connections do
                yield new X4Galaxy.Connection(connection.XElement)
        |]

    let X4GalaxyCore = X4Galaxy.Load(X4GalaxyFileCore)
    let X4GalaxySplit = X4GalaxyDiff.Load(X4GalaxyFileSplit)
    let X4GalaxyTerran = X4GalaxyDiff.Load(X4GalaxyFileTerran)
    let X4GalaxyPirate = X4GalaxyDiff.Load(X4GalaxyFilePirate)
    let X4GalaxyBoron = X4GalaxyDiff.Load(X4GalaxyFileBoron)

    Array.toList
    <| Array.concat [
        X4GalaxyCore.Macro.Connections
        loadFromDiff X4GalaxySplit
        loadFromDiff X4GalaxyTerran
        loadFromDiff X4GalaxyPirate
        loadFromDiff X4GalaxyBoron
    ]

// The 'index' xml files contain 'entries' that are used to map an entity name (component or macro)
// to a file name containing the definition of that entity.
// This function will load the index entries across all the DLCs and core game for a specific index file,
// such as 'macros.xml' or 'components.xml'.
let LoadIndexes (index: string) =
    [
        for dlc in ContentDirectories do
            let X4IndexMacrosFile = getDlcDirectory dlc "/index/" + index

            if File.Exists X4IndexMacrosFile then
                X4IndexMacro.Load(X4IndexMacrosFile).Entries
                |> Array.map (fun entry -> {
                    Name = entry.Name
                    File = entry.Value.Replace("\\", "/") + ".xml"
                    DLC = dlc
                })
            else
                printfn "Warning: No index macros file found for %s" dlc
                [||]
    ]
    |> Array.concat


let AllIndexMacros = LoadIndexes "macros.xml"
let AllComponentMacros = LoadIndexes "components.xml"

let allRegionDefinitions = X4RegionDefinitions.Load(X4RegionDefinitionsFile)
let regionYields = X4RegionYields.Load(X4RegionYieldsFile)

// Read all thge stations and products from the core game and the DLCs.
let allStations, allProducts =
    // Helper functions. Extract the stations from the 'add' section of a DLCs god diff/mod file.
    // While there are many 'add' sections, we're only interested in the one that that has the selectior '//god/stations'
    let getStationsFromDiff (diff: X4GodMod.Add[]) =
        let stationsAdd =
            Array.filter (fun (add: X4GodMod.Add) -> add.Sel = "/god/stations") diff

        [
            for stations in stationsAdd do
                for station in stations.Stations do
                    yield new X4WorldStart.Station(station.XElement)
        ]

    let getProductFromDiff (diff: X4GodMod.Add[]) =
        let productsAdd =
            Array.filter (fun (add: X4GodMod.Add) -> add.Sel = "/god/products") diff

        [
            for products in productsAdd do
                for product in products.Products do
                    yield new X4WorldStart.Product(product.XElement)
        ]

    let X4GodCore = X4WorldStart.Load(X4GodFileCore)
    let X4GodSplit = X4GodMod.Load(X4GodFileSplit)
    let X4GodTerran = X4GodMod.Load(X4GodFileTerran)
    let X4GodPirate = X4GodMod.Load(X4GodFilePirate)
    let X4GodBoron = X4GodMod.Load(X4GodFileBoron)

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
    XenonShipyard("sector", "Cluster_46_sector001_macro") // Morningstar IV
    XenonWharf("sector", "Cluster_46_sector001_macro")

    XenonShipyard("sector", "Cluster_100_sector001_macro") // Asteroid belt
    XenonShipyard("sector", "Cluster_109_sector001_macro") // Uranus

    XenonWharf("sector", "Cluster_413_sector001_macro") // Tharka Ravine IV: Tharkas Fall
]

// ===== FINISHED LOADING DATA FROM XML FILES =====

// Gates are linked to a zone by using one of the following as a reference. So by looking
// for these references in the zone file, we can find the gates in a zone.
// I don't think we actually need this. More investigation seems to suggest that a gate is
// identified by a zone connection ref="gates" instead. If correct, we can remove this.
let gateMacros = [
    "props_gates_orb_accelerator_01_macro", "props_gates_anc_gate_macro", "props_ter_gate_01_macro"
]



// Get all the factions defined in the speficied DLC
let dlcFactions dlc = X4.WriteModfiles.dlcFactions dlc
// Filter the territories list to only include those that are in the specified DLC
let dlcTerritories dlc =
    let factions = dlcFactions dlc
    territories |> List.filter (fun t -> List.contains t.faction factions)

// Given a cluster name, return the X4Cluster object representing it.
let findCluster (clusterName: string) =
    AllClusters |> List.tryFind (fun cluster -> cluster.Name =? clusterName)

let getClusterMacroConnectionsByType connectionType (cluster: X4Cluster.Macro) =
    cluster.Connections
    |> Array.toList
    |> List.filter (fun connection -> connection.Ref = connectionType)

// Given a cluster name, find it, and then return all of it's connections of the specific type in a list.
// Note that here, unlike in other places, the type is pluralised. eg, don't search for 'sector', use 'sectors'
// Returns empty list if no sectors found.
let getClusterConnectionsByType connectionType clusterName =
    findCluster clusterName
    |> Option.map (fun cluster -> getClusterMacroConnectionsByType connectionType cluster)
    |> Option.defaultValue []

// Given a cluster name, return all the X4Sector objects in a list.
let findSectorsInCluster (cluster: string) =
    getClusterConnectionsByType "sectors" cluster
    |> List.map (fun connection -> Option.defaultValue "no_sector_name" connection.Macro.Ref)
    |> List.map (fun sector -> sector.ToLower()) // Lower case for consistency

let getFactionClusters (faction: string) =
    territories
    |> List.filter (fun record -> record.faction = faction)
    |> List.map (fun record -> record.cluster)

let getFactionSectors (faction: string) =
    getFactionClusters faction |> List.collect findSectorsInCluster

// Given a sector name, which cluster does it belong to?
let findClusterFromSector (sector: string) =
    AllClusters
    |> List.tryFind
        (
        // For each cluster, we'll check if there's a connection to this sector.
        fun cluster ->
            getClusterMacroConnectionsByType "sectors" cluster
            |> List.exists (fun c -> Option.defaultValue "no_sector_name" c.Macro.Ref =? sector))
    // If we actually found a match, change the return value from Some Cluster to Some Cluster.Name
    |> Option.map (fun cluster -> cluster.Name)

// Using the data in sector.xml, which is represented by the X4Sector type, find the name of
// the sector given the name of the zone. the zone is stored as a connection in the sector definition.
let findSectorFromZone (zone: string) =
    // allSectors is a list of secto Macros. Each macro represents a sector. In that sector we'll find connections.
    // Each connection will have zero or more zones for use to check. So we try find a macro that contains a zone with the name we're looking for.
    // Then return the name of that macro.
    allSectors
    |> List.tryFind (fun sector ->
        sector.Connections
        |> Array.tryFind (fun connection ->
            connection.Ref = "zones"
            && connection.Macro.Connection = "sector"
            && connection.Macro.Ref =? zone)
        |> Option.isSome)
    |> Option.map (fun sector -> sector.Name.ToLower()) // return the sector name, but in lower case, as the case varies in the files. I prefer to make it consistent

let findClusterFromLocation (locationClass: string) (locationMacro: string) =
    match locationClass with
    | "zone" ->
        findSectorFromZone locationMacro
        |> Option.map findClusterFromSector
        |> Option.flatten
    | "sector" -> findClusterFromSector locationMacro
    | "cluster" -> Some locationMacro
    | _ -> None

// Explicit check for whether we've ALLOWED a faction in a cluster in our territory mapping.
// For most factions this is a lot less than what is in the base game.
let isFactionInCluster (faction: string) (cluster: string) =
    territories
    |> List.exists (fun record -> record.faction = faction && record.cluster =? cluster)

// This function returns whether a faction is ALLOWED to be in the sector as per our mod rules
let isFactionInSector (faction: string) (sector: string) =
    findClusterFromSector sector
    |> Option.map (fun cluster -> isFactionInCluster faction cluster)
    |> Option.defaultValue false

// Have we ALLOWED the faction to be in this specific zone?
let isFactionInZone (faction: string) (zone: string) =
    match findSectorFromZone zone with
    | None -> false
    | Some sector -> isFactionInSector faction sector

// Given any location name and class, return whether the faction is ALLOWED to be in that location.
let isFactionInLocation (faction: string) (location: string) (locationClass: string) =
    match locationClass with
    | "galaxy" -> true // well, if the class is galaxy, then definitely
    | "sector" -> isFactionInSector faction location
    | "cluster" -> isFactionInCluster faction location
    | "zone" -> isFactionInZone faction location
    | _ -> failwith ("Unhandled location class in job: " + locationClass)


let findFactionFromCluster (cluster: string) =
    territories
    |> List.tryFind (fun record -> record.cluster =? cluster)
    |> Option.map (fun record -> record.faction)

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
    |> Option.map (fun connection ->
        connection.Offset.Value.Position.X, connection.Offset.Value.Position.Y, connection.Offset.Value.Position.Z)
    |> Option.get

// this function wil take an XElement, and return the integer version of the value.
// It will handle both decimals and floating point strings in scientific notation.
// We need this because the Boron DLC, for some reason, has positions in scientific notation.
// eg, 1.234e+005
let getIntValue (element: string) = int (float element)

let parsePosition (element: XElement) =
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
        |> Option.get // Crap out if we can't find the cluster. This should never happen, and if it does, it means data has changed.

    // Now look for the connection to the sector in the cluster, and get the position for that connection.
    cluster.Connections
    |> Array.tryFind (fun connection -> connection.Ref = "sectors" && connection.Macro.Ref =?? sectorName)
    |> Option.map (fun connection ->
        connection.Offset
        |> Option.map (fun offset -> parsePosition offset.Position.XElement))
    |> Option.flatten
    // The connection for the sector may not have had a position, in which case it defaults to cluster center: ie, 0,0,0
    |> Option.defaultValue (0, 0, 0)


// Get all the safe sectors in the game. that is, sectors where factions exist, rather than Xenon or neutral
let getSafeSectors =
    allSectors
    |> List.filter
        (
        // Filter to every sector that is in a cluster mentioned in the territories list. Those are our safe clusters.
        fun sector ->
            let cluster = findClusterFromSector sector.Name
            territories |> List.exists (fun record -> cluster =?? record.cluster))

// Get all the UNSAFE sectors in the game. That's the sectors that are Xenon in our mod, or neutral.
let getUnsafeSectors = allSectors |> List.except getSafeSectors

let selectRandomSector () =
    allSectors.[rand.Next(allSectors.Length)]

let selectRandomSafeSector () =
    getSafeSectors.[rand.Next(getSafeSectors.Length)]

let selectRandomUnsafeSector () =
    getUnsafeSectors.[rand.Next(getUnsafeSectors.Length)]




// Each DLC is in a separate directory; and the different types of files describing ships
// and equipment are in a different set of subdirs off of that base of subtype.
// Quick helper function with some common code to pull in all the files from these
// subdirs from each DLC and merge in.
let getDlcXmlFiles dataDir =
    getDlcDirectories dataDir
    |> List.toArray
    |> Array.collect (fun dir ->
        try
            printfn $"Loading XML files from {dir}"
            // Recursively get all XML files in directory and subdirectories
            Directory.GetFiles(dir, "*.xml", SearchOption.AllDirectories)
        with ex ->
            printfn $"Failed to load files from {dir}. Directory may not exist."
            [||])



// Get all the assets defined in the core game and the DLCs. This includes
// equipment for ships, as well as miscellaneous assets like wares, adsigns, etc
let (allAssets: Asset list) =
    // For ship equipment, it's not enough to look at the AllComponentMacros.
    // This is because the component file doesn't contain all items. Instead, the index macro
    // defines all items, and them points to a reference in the component file that might be shared.
    // eg: Boron small shields. Index contains all shield entries, but each points to the same shared
    // component entry of
    // <entry name="shield_bor_s_standard_01" value="extensions\ego_dlc_boron\assets\props\surfaceelements\shield_bor_s_standard_01" />

    // So, for each entry in the index file, filter down to just those that refer
    // to files in the X4EquipmentDirectory. These entries point to a specific component
    // file, which we need to load and parse.
    // In the macro file, we'll need to find the reference to the component
    // We use this component reference to look up the actual component file in the component index.
    AllIndexMacros
    |> Array.filter (fun index -> index.File.ToLower().Contains("assets/"))
    |> Array.map (fun index ->
        printfn "Loading equipment: %s" index.File
        // Load the referenced file.
        let fileName = X4UnpackedDataFolder + "/" + index.File.Replace("\\", "/")

        if File.Exists fileName then
            Some(index, X4ShipsMacro.Load fileName)
        else
            printfn "Warning: Component file %s not found." fileName
            None)
    |> Array.choose id
    |> Array.toList
    |> List.map (fun (index, macro) ->
        option {
            // We have the macro loaded, so now find the component the macro references,
            // and look it up in the components index, and THEN finally load that component.
            let! componentEntry =
                AllComponentMacros
                |> Array.tryFind (fun componentEntry -> componentEntry.Name =? macro.Macro.Component.Ref)

            let componentFilename =
                X4UnpackedDataFolder + "/" + componentEntry.File.Replace("\\", "/")

            printfn "Found component %s -> %s" componentEntry.Name componentFilename
            return index, macro.Macro.Name, componentFilename
        }

    )
    |> List.choose id
    |> List.map (fun (index, name, componentFilename) ->
        try
            // Now parse the file using the X4Equipment type.
            let parsed = X4Equipment.Load(componentFilename)
            // Ensure the name is set correctly in the XElement. Should be index, not component name
            // parsed.Component.XElement.SetAttributeValue("name", name.Trim())
            // printfn "Loaded equipment: %-35s %-20s from %s" parsed.Component.Name parsed.Component.Class x
            Some {
                Name = name.Trim() // Ensure the name is set correctly. Should be index, not component name
                File = componentFilename
                DLC = index.DLC
                Class = parsed.Component.Class.Trim()
                Asset = parsed.Component
            }
        with ex ->
            printfn $"\nError loading equipment: {componentFilename}: {ex.Message}"
            // try find root of parse error:
            let raw = System.Xml.Linq.XDocument.Load(componentFilename)
            printfn "Children of <components>:"
            raw.Root.Elements() |> Seq.iter (fun e -> printfn "- %s" e.Name.LocalName)
            printfn "End of children.\n"
            None)
    |> List.choose id


let allAssetsByClass =
    // Group all the assets by their class, so we can easily find them later.
    allAssets |> List.groupBy (fun asset -> asset.Class) |> Map.ofList // Convert to a map for easy lookup

let allAssetClasses =
    // Get all the unique classes of assets, sorted alphabetically.
    allAssets
    |> List.distinctBy (fun asset -> asset.Class)
    |> List.map (fun asset -> asset.Class)
    |> List.sort


let dump_sectors (sectors: X4Sector.Macro list) =
    for sector in sectors do
        printfn "macro: %s," (sector.Name.ToLower())

let dumpRegionDefinitions () =
    for region in allRegionDefinitions.Regions do
        printfn "macro: %s," (region.Name.ToLower())

let dumpRegionYields () =
    printfn "Discovered Region Yields:"

    for ware in regionYields.Resources do
        printfn "\nResource: %s:" (ware.Ware.ToLower())

        for ryield in ware.Yields do
            printfn
                "   %12s: yield: %6M over %6i minutes = %7.2f/h/km^2"
                ryield.Name
                ryield.Resourcedensity
                ryield.Replenishtime
                ((float (ryield.Resourcedensity) / float (ryield.Replenishtime)) * 60.0)
