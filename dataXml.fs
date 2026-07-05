/// <summary>
/// The XML edge of the data layer: file paths for the unpacked game data, the
/// FSharp.Data XmlProvider definitions used to parse it, and the index files that
/// map entity names to their definition files.
/// All XmlProvider types live here; by the end of the refactor nothing outside
/// the X4.Data modules should reference them.
/// Deliberately NOT [<AutoOpen>]: only the X4.Data.* modules open this explicitly,
/// so `open X4.Data` in a logic module cannot see any provider types.
/// </summary>
module X4.Data.Xml

open Microsoft.FSharp.Core
open FSharp.Data
open System
open System.IO

// The DLC extension folders, in load order. Adding a future DLC means adding it
// here plus one path in each of the per-file-kind lists below.
let DlcDirectories = [
    "ego_dlc_split"
    "ego_dlc_terran"
    "ego_dlc_pirate"
    "ego_dlc_boron"
    "ego_dlc_timelines"
]

// The core game ("") plus every DLC - the content sources for index loading.
let ContentDirectories = "" :: DlcDirectories

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

// Path to a file inside a DLC's extension folder.
let extensionPath dlc relPath =
    X4UnpackedDataFolder + "/extensions/" + dlc + "/" + relPath

[<Literal>]
let X4GodFileCore = X4UnpackedDataFolder + "/libraries/god.xml" // also the provider sample

// The DLC god files, in load order. They are diffs (X4GodMod format), unlike the core file.
let X4GodDlcFiles = [ for dlc in DlcDirectories -> extensionPath dlc "libraries/god.xml" ]

// The map files: core first, then the DLCs in load order. Each DLC names its own
// files differently, so these lists stay explicit. The core file of each kind is a
// [<Literal>] because it doubles as the XmlProvider compile-time sample.
[<Literal>]
let X4ClusterFileCore = X4UnpackedDataFolder + "/maps/xu_ep2_universe/clusters.xml"

let X4ClusterFiles = [
    X4ClusterFileCore
    extensionPath "ego_dlc_split" "maps/xu_ep2_universe/dlc4_clusters.xml"
    extensionPath "ego_dlc_terran" "maps/xu_ep2_universe/dlc_terran_clusters.xml"
    extensionPath "ego_dlc_pirate" "maps/xu_ep2_universe/dlc_pirate_clusters.xml"
    extensionPath "ego_dlc_boron" "maps/xu_ep2_universe/dlc_boron_clusters.xml"
    extensionPath "ego_dlc_timelines" "maps/xu_ep2_universe/dlc7_clusters.xml"
]

[<Literal>]
let X4SectorFileCore = X4UnpackedDataFolder + "/maps/xu_ep2_universe/sectors.xml"

let X4SectorFiles = [
    X4SectorFileCore
    extensionPath "ego_dlc_split" "maps/xu_ep2_universe/dlc4_sectors.xml"
    extensionPath "ego_dlc_terran" "maps/xu_ep2_universe/dlc_terran_sectors.xml"
    extensionPath "ego_dlc_pirate" "maps/xu_ep2_universe/dlc_pirate_sectors.xml"
    extensionPath "ego_dlc_boron" "maps/xu_ep2_universe/dlc_boron_sectors.xml"
    extensionPath "ego_dlc_timelines" "maps/xu_ep2_universe/dlc7_sectors.xml"
]

[<Literal>]
let X4ZoneFileCore = X4UnpackedDataFolder + "/maps/xu_ep2_universe/zones.xml"

let X4ZoneFiles = [
    X4ZoneFileCore
    extensionPath "ego_dlc_split" "maps/xu_ep2_universe/dlc4_zones.xml"
    extensionPath "ego_dlc_terran" "maps/xu_ep2_universe/dlc_terran_zones.xml"
    extensionPath "ego_dlc_pirate" "maps/xu_ep2_universe/dlc_pirate_zones.xml"
    extensionPath "ego_dlc_boron" "maps/xu_ep2_universe/dlc_boron_zones.xml"
    extensionPath "ego_dlc_timelines" "maps/xu_ep2_universe/dlc7_zones.xml"
]

[<Literal>]
let X4GalaxyFileCore = X4UnpackedDataFolder + "/maps/xu_ep2_universe/galaxy.xml"

[<Literal>] // the DLC galaxy files are in DIFF format, so they need a different type provider sample.
let X4GalaxyFileSplit =
    X4UnpackedDataFolder
    + "/extensions/ego_dlc_split/maps/xu_ep2_universe/galaxy.xml"

// The DLC galaxy diff files, in load order (every DLC uses the same file name).
let X4GalaxyDlcFiles = [ for dlc in DlcDirectories -> extensionPath dlc "maps/xu_ep2_universe/galaxy.xml" ]

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

// ====== LOAD DATA FROM XML FILES ======

// Generate a path to a sub directory for either base game of specific DLC
let getDlcDirectory dlc subDir =
    match dlc with
    | "" -> X4UnpackedDataFolder + "/" + subDir // base game files are in the root of the unpacked data folder.
    | dir -> X4UnpackedDataFolder + "/extensions/" + dir + subDir


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
