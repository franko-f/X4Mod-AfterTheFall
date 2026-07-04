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

let rand = new Random(12345) // Seed the random number generator so we get the same results each time as long as were not changing code.



// Load the cluster data from each individual core/expansion cluster XML file. We'll combine them in to one list.
// Convinience functions to search/manipulate these lists are defined below.
let AllClusters =
    let X4ClusterCore = X4Cluster.Load(X4ClusterFileCore)
    let X4ClusterSplit = X4Cluster.Load(X4ClusterFileSplit)
    let X4ClusterTerran = X4Cluster.Load(X4ClusterFileTerran)
    let X4ClusterPirate = X4Cluster.Load(X4ClusterFilePirate)
    let X4ClusterBoron = X4Cluster.Load(X4ClusterFileBoron)
    let X4ClusterTimelines = X4Cluster.Load(X4ClusterFileTimelines)

    Array.toList
    <| Array.concat [
        X4ClusterCore.Macros
        X4ClusterSplit.Macros
        X4ClusterTerran.Macros
        X4ClusterPirate.Macros
        X4ClusterBoron.Macros
        X4ClusterTimelines.Macros
    ]

// Load the sector data from each individual sector file. We'll combine them in to one list.
let allSectors =
    let X4SectorCore = X4Sector.Load(X4SectorFileCore)
    let X4SectorSplit = X4Sector.Load(X4SectorFileSplit)
    let X4SectorTerran = X4Sector.Load(X4SectorFileTerran)
    let X4SectorPirate = X4Sector.Load(X4SectorFilePirate)
    let X4SectorBoron = X4Sector.Load(X4SectorFileBoron)
    let X4SectorTimelines = X4Sector.Load(X4SectorFileTimelines)

    Array.toList
    <| Array.concat [
        X4SectorCore.Macros
        X4SectorSplit.Macros
        X4SectorTerran.Macros
        X4SectorPirate.Macros
        X4SectorBoron.Macros
        X4SectorTimelines.Macros
    ]

let allZones =
    let X4ZoneCore = X4Zone.Load(X4ZoneFileCore)
    let X4ZoneSplit = X4Zone.Load(X4ZoneFileSplit)
    let X4ZoneTerran = X4Zone.Load(X4ZoneFileTerran)
    let X4ZonePirate = X4Zone.Load(X4ZoneFilePirate)
    let X4ZoneBoron = X4Zone.Load(X4ZoneFileBoron)
    let X4ZoneTimelines = X4Zone.Load(X4ZoneFileTimelines)

    Array.toList
    <| Array.concat [
        X4ZoneCore.Macros
        X4ZoneSplit.Macros
        X4ZoneTerran.Macros
        X4ZonePirate.Macros
        X4ZoneBoron.Macros
        X4ZoneTimelines.Macros
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
    let X4GalaxyTimelines = X4GalaxyDiff.Load(X4GalaxyFileTimelines)

    Array.toList
    <| Array.concat [
        X4GalaxyCore.Macro.Connections
        loadFromDiff X4GalaxySplit
        loadFromDiff X4GalaxyTerran
        loadFromDiff X4GalaxyPirate
        loadFromDiff X4GalaxyBoron
        loadFromDiff X4GalaxyTimelines
    ]


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

    territories
    |> List.filter (fun t ->
        // This is a little messy: A territory may explicitly set a DLC. Check for this.
        match t.dlc with
        | Some territoryDlc -> territoryDlc = dlc
        | None -> List.contains t.faction factions)

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

let getTerritoryFromClusterName (clusterName: string) =
    territories |> List.tryFind (fun record -> record.cluster =? clusterName)

// Given a faction and a territory record (encapsulating a cluster and the faction that owns it), return all the sectors in that territory that belong to the faction. Empty list if none.
let getFactionSectorsInTerritory (faction: string) (territory: Territory) =
    if territory.faction <> faction then
        // If the territory doesn't belong to the faction, then no sectors do.
        []
    else if territory.sectors.IsEmpty then
        // If sectors list is empty, then all sectors belong to the faction
        findSectorsInCluster territory.cluster
    else
        // Otherwise, return the sectors explicitly listed in the territory.
        territory.sectors

let getFactionSectors (faction: string) =
    territories
    |> List.collect (fun territory -> getFactionSectorsInTerritory faction territory)

let getFactionSectorsInCluster (faction: string) (cluster: string) =
    territories
    |> List.filter (fun territory -> territory.cluster =? cluster)
    |> List.collect (fun territory -> getFactionSectorsInTerritory faction territory)

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
// Cluster is not fine grained enough for most things (we can assign down to sector level), but
// jobs are often defined at the cluster level. This should not be used for most things.
let isFactionInCluster (faction: string) (cluster: string) =
    territories
    |> List.exists (fun record -> record.faction = faction && record.cluster =? cluster)

// This function returns whether a faction is ALLOWED to be in the sector as per our mod rules
let isFactionInSector (faction: string) (sector: string) =
    // Find the cluster the sector belongs to, THEN check if sector is in the list of faction sectors in that cluster.
    findClusterFromSector sector
    |> Option.map (fun cluster -> getFactionSectorsInCluster faction cluster |> List.exists (fun s -> s =? sector))
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

// Sectors are a bit more complicated, as a faction might only have *some* of the sectors in a cluster.
let findFactionFromSector (sector: string) =
    match findClusterFromSector sector with
    | Some cluster ->
        // TODO: A cluster might be listed more than once, for factions like MIN. Probably not important in our use case.
        // Find the territory record, then check if the sector is in the list of sectors for that territory.
        territories
        |> List.filter (fun record -> record.cluster =? cluster)
        |> List.tryPick (fun territory ->
            if List.contains sector territory.sectors then
                // Is this sector explicitly listed? then it belongs to the territories faction.
                Some territory.faction
            else if territory.sectors.IsEmpty then
                // if the list is empty, it means the faction owns the entire cluster
                Some territory.faction
            else
                // otherwise, it's not in the list, for this specific territory record.
                None)
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

