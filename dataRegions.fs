/// <summary>
/// The mining-resource side of the data layer: the X4 9.0 regionyields.xml
/// vocabulary used to compose resource area refs, and the vanilla mapdefaults
/// datasets that determine what diff operation each sector needs.
/// (Phase G of the refactor will also move the WRITING of the mod's mapdefaults
/// and cluster region diffs here.)
/// </summary>
[<AutoOpen>]
module X4.Data.Regions

open System.Xml.Linq
open X4.Types
open X4.Utilities
open X4.Data.Xml

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
let private resourceAreaVocabulary =
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
let private mapDefaultsDocs =
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
let private properCaseSectorName (sector: string) =
    allSectors
    |> List.tryFind (fun s -> s.Name =? sector)
    |> Option.map (fun s -> s.Name)
    |> Option.defaultWith (fun () -> failwithf "Unknown sector macro: %s" sector)

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







