/// <summary>
/// The XML edge of the data layer: file paths for the unpacked game data, the
/// FSharp.Data XmlProvider definitions used to parse it, and the index files that
/// map entity names to their definition files.
/// All XmlProvider types live here; by the end of the refactor nothing outside
/// the X4.Data modules should reference them.
/// </summary>
[<AutoOpen>]
module X4.Data.Xml

open Microsoft.FSharp.Core
open FSharp.Data
open System
open System.IO

let ContentDirectories = [
    ""
    "ego_dlc_split"
    "ego_dlc_terran"
    "ego_dlc_pirate"
    "ego_dlc_boron"
    "ego_dlc_timelines"
]

// Game data is unpacked into a subdirectory per game version (e.g. X4_unpacked_data/9.0)
// so that different versions can be kept side by side and compared. This selects the
// version the generator (and all XmlProvider compile-time samples) build against.
[<Literal>]
let X4DataVersion = "9.0"

[<Literal>]
let X4UnpackedDataFolder = __SOURCE_DIRECTORY__ + "/X4_unpacked_data/" + X4DataVersion

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

let X4GodFileTimelines =
    X4UnpackedDataFolder + "/extensions/ego_dlc_timelines/libraries/god.xml" // Core game data.

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

let X4ClusterFileTimelines =
    X4UnpackedDataFolder
    + "/extensions/ego_dlc_timelines/maps/xu_ep2_universe/dlc7_clusters.xml"

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

let X4SectorFileTimelines =
    X4UnpackedDataFolder
    + "/extensions/ego_dlc_timelines/maps/xu_ep2_universe/dlc7_sectors.xml"

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

let X4ZoneFileTimelines =
    X4UnpackedDataFolder
    + "/extensions/ego_dlc_timelines/maps/xu_ep2_universe/dlc7_zones.xml"

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

let X4GalaxyFileTimelines =
    X4UnpackedDataFolder
    + "/extensions/ego_dlc_timelines/maps/xu_ep2_universe/galaxy.xml"

// Regions for mining fields
[<Literal>]
let X4RegionDefinitionsFile =
    X4UnpackedDataFolder + "/libraries/region_definitions.xml"

// NOTE: 9.0 restructured libraries/regionyields.xml completely: it now defines the
// vocabularies (boundaries, yield tiers, gather speeds) that compose per-sector
// 'resource area' refs like sphere_large_ore_high_slow, referenced from mapdefaults.xml.
// It is parsed with plain XDocument (see resource area code) rather than a type provider,
// so future schema tweaks fail at runtime with a clear message instead of breaking
// unrelated code at compile time.

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
