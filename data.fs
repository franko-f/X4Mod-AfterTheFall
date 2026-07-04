/// <summary>
/// This module contains data around factions, sectors and rules we'll use to
/// generate our new universe.
/// </summary>

[<AutoOpen>]
module X4.Data.Core

open Microsoft.FSharp.Core
open FSharp.Data
open X4.Utilities
open System
open System.IO
open System.Xml
open System.Xml.Linq

open X4.Territories
open X4.Data.Xml


let allRegionDefinitions = X4RegionDefinitions.Load(X4RegionDefinitionsFile)

// ===== 9.0 RESOURCE AREAS =====
// As of X4 9.0, minable resources are no longer defined by <resources> nodes in
// region definitions. Instead, each sector's <dataset> in libraries/mapdefaults.xml
// lists <resourcearea> entries, whose 'ref' composes vocabulary defined in
// libraries/regionyields.xml as sphere_{size}_{ware}_{yield}_{gatherspeed},
// e.g. 'sphere_large_ore_high_average'.
// Regions placed in the cluster maps still provide the physical asteroid/gas fields;
// resource areas roam within them and fill them with minable yield. BOTH halves are
// required, and their wares must match, or you get empty rocks / invisible yields.

// The vocabularies from regionyields.xml, used to validate the parts we compose
// resource area refs from. Parsed with plain XDocument rather than a type provider,
// so any future schema drift fails here with a clear runtime error instead of
// breaking unrelated code at compile time.
let resourceAreaVocabulary =
    let doc =
        XDocument.Load(X4UnpackedDataFolder + "/libraries/regionyields.xml")

    let ids (parent: string) (child: string) =
        doc.Root.Element(XName.Get parent).Elements(XName.Get child)
        |> Seq.map (fun e -> e.Attribute(XName.Get "id").Value)
        |> Set.ofSeq

    let wares =
        doc.Root.Element(XName.Get "yields").Elements(XName.Get "yield")
        |> Seq.collect (fun y -> y.Elements(XName.Get "ware"))
        |> Seq.map (fun w -> w.Attribute(XName.Get "id").Value)
        |> Set.ofSeq

    {|
        Boundaries = ids "boundaries" "boundary" // e.g. "sphere_large"
        Yields = ids "yields" "yield" // verylow, low, medium, high, veryhigh
        Wares = wares // ore, silicon, ice, nividium, hydrogen, helium, methane, rawscrap, rawkhaakscrap
        GatherSpeeds = ids "gatherspeeds" "gatherspeed" // veryslow, slow, average, fast, veryfast
    |}

// Compose a resource area ref from its parts, validating each part against the
// regionyields.xml vocabulary so a typo (or a game data change) fails generation
// loudly instead of producing a silently dead resource area in game.
let makeResourceAreaRef (size: string) (ware: string) (yieldTier: string) (speed: string) =
    let v = resourceAreaVocabulary
    let boundary = "sphere_" + size

    if not (v.Boundaries.Contains boundary) then
        failwithf "Resource area size '%s' is not a boundary in regionyields.xml: %A" size (Set.toList v.Boundaries)

    if not (v.Wares.Contains ware) then
        failwithf "Resource area ware '%s' is not in regionyields.xml: %A" ware (Set.toList v.Wares)

    if not (v.Yields.Contains yieldTier) then
        failwithf "Resource area yield '%s' is not in regionyields.xml: %A" yieldTier (Set.toList v.Yields)

    if not (v.GatherSpeeds.Contains speed) then
        failwithf "Resource area speed '%s' is not a gatherspeed in regionyields.xml: %A" speed (Set.toList v.GatherSpeeds)

    $"{boundary}_{ware}_{yieldTier}_{speed}"

// The vanilla mapdefaults file for the core game and for each DLC we generate
// resources for. Diff patches for a DLC's sectors must go in that DLC's own
// extensions/<dlc>/libraries/mapdefaults.xml so they only load when the DLC exists.
let mapDefaultsDocs =
    Map [
        "core", X4UnpackedDataFolder + "/libraries/mapdefaults.xml"
        "split", X4UnpackedDataFolder + "/extensions/ego_dlc_split/libraries/mapdefaults.xml"
        "terran", X4UnpackedDataFolder + "/extensions/ego_dlc_terran/libraries/mapdefaults.xml"
        "pirate", X4UnpackedDataFolder + "/extensions/ego_dlc_pirate/libraries/mapdefaults.xml"
        "boron", X4UnpackedDataFolder + "/extensions/ego_dlc_boron/libraries/mapdefaults.xml"
    ]
    |> Map.map (fun _ file -> XDocument.Load file)

// Our code works with lowercased sector macro names throughout, but XML diff
// selectors are CASE SENSITIVE, and mapdefaults/sectors.xml use names like
// 'Cluster_14_Sector001_macro'. Recover the canonical casing from sectors.xml
// before interpolating a name into a selector.
let properCaseSectorName (sector: string) =
    allSectors
    |> List.tryFind (fun s -> s.Name =? sector)
    |> Option.map (fun s -> s.Name)
    |> Option.defaultWith (fun () -> failwithf "Unknown sector macro: %s" sector)

// How a sector is represented in the vanilla mapdefaults file, which determines the
// diff operation needed to add resource areas to it. Each case carries the
// canonically cased macro name to use in the selector.
type MapDefaultsDatasetState =
    | HasResourceAreas of string // dataset exists and already has a <resourceareas> node
    // dataset exists with <properties> but no <resourceareas>. The <properties> children
    // are schema ordered (xs:sequence in libraries.xsd: boundaries, identification,
    // resources, resourceareas, sounds, area, ...), so a plain append would put our node
    // after sounds/area/access and fail validation. The second value is the existing
    // child to insert after (pos="after"), or None to prepend as the first child.
    | HasProperties of string * string option
    | NoDataset of string // the sector has no dataset in the file at all

let getMapDefaultsDatasetState (dlc: string) (sector: string) =
    let dataset =
        mapDefaultsDocs.[dlc].Root.Elements(XName.Get "dataset")
        |> Seq.tryFind (fun d -> d.Attribute(XName.Get "macro").Value =? sector)

    match dataset with
    | None -> NoDataset(properCaseSectorName sector)
    | Some dataset ->
        let macro = dataset.Attribute(XName.Get "macro").Value // use the file's own casing

        match dataset.Element(XName.Get "properties") with
        | null -> failwithf "mapdefaults dataset '%s' has no <properties> node: unhandled shape" macro
        | properties when properties.Element(XName.Get "resourceareas") <> null -> HasResourceAreas macro
        | properties ->
            // Anchor on the last existing child that the schema allows before <resourceareas>.
            let preceders = set [ "boundaries"; "identification"; "resources" ]

            let anchor =
                properties.Elements()
                |> Seq.filter (fun e -> preceders.Contains e.Name.LocalName)
                |> Seq.tryLast
                |> Option.map (fun e -> e.Name.LocalName)

            HasProperties(macro, anchor)






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

