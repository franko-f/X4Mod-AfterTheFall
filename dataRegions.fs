/// <summary>
/// The mining-resource side of the data layer: the X4 9.0 regionyields.xml
/// vocabulary used to compose resource area refs, and the vanilla mapdefaults
/// datasets that determine what diff operation each sector needs.
/// Also renders and writes the mod's mapdefaults and cluster region diffs.
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
// resources for. These are consulted only to decide each sector's diff op shape;
// the ops themselves ALL go in the mod's core libraries/mapdefaults.xml. The game
// merges every extension's own mapdefaults into one patchable document (like
// defaults.xml) and never reads nested extensions/<dlc>/libraries copies - verified
// in game: nested cluster map diffs apply, nested mapdefaults diffs are ignored,
// while the core file's access ops patch Terran DLC datasets successfully.
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









// ==== WRITERS ====
// All the mod's mining-resource XML is produced here from the pure directive records
// in X4.Types. The interpolated string templates are indented for readability here;
// write_xml_file reformats the whole document uniformly on save.

// One cluster map <add> operation placing a visual mining field region.
let private regionPlacementXml (placement: RegionPlacement) =
    let x, y, z = placement.Position
    let cluster = placement.Cluster
    let regionName = placement.Name
    let region = placement.RegionRef

    let xml =
        $"""
    <add sel="/macros/macro[@name='%s{cluster}']/connections">
      <connection name="%s{regionName}_connection" ref="regions">
        <offset>
          <position x="{x}" y="{y}" z="{z}" />
        </offset>
        <macro name="%s{regionName}_macro">
          <component connection="cluster" ref="standardregion" />
          <properties>
            <region ref="{region}" />
          </properties>
        </macro>
      </connection>
    </add>
    """

    XElement.Parse(xml)

// Write a DLC's cluster map diff placing the visual mining field regions.
let writeClusterRegions (dlc: string) (filename: string) (placements: RegionPlacement list) =
    // Create the new XML Diff document to contain our region additions
    let diff = X4.WriteModfiles.newDiff ()

    // Now add the region changes, one by one, to the the xml diff.
    for placement in placements do
        diff.Add(regionPlacementXml placement)

    X4.WriteModfiles.write_xml_file dlc filename diff

// One mapdefaults <add> operation granting a sector its resource areas. The shape of
// the operation depends on how the sector appears in the vanilla mapdefaults file:
// an existing <resourceareas> node gets children appended; a dataset without one gets
// the whole node; a sector with no dataset at all (rare, e.g. Cluster_714) gets a
// complete new dataset added to the document root.
let private sectorResourceAreasXml (grant: SectorResourceGrant) =
    let areaLines =
        grant.Areas
        |> List.map (fun (ref, amount) -> $"""        <resourcearea amount="{amount}" ref="{ref}" />""")
        |> String.concat "\n"

    let xml =
        match grant.State with
        | HasResourceAreas macro ->
            $"""
    <add sel="/defaults/dataset[@macro='{macro}']/properties/resourceareas">
{areaLines}
    </add>
    """
        | HasProperties(macro, Some anchor) ->
            // insert after the anchor child to respect the schema's element order
            $"""
    <add sel="/defaults/dataset[@macro='{macro}']/properties/{anchor}" pos="after">
      <resourceareas>
{areaLines}
      </resourceareas>
    </add>
    """
        | HasProperties(macro, None) ->
            $"""
    <add sel="/defaults/dataset[@macro='{macro}']/properties" pos="prepend">
      <resourceareas>
{areaLines}
      </resourceareas>
    </add>
    """
        | NoDataset macro ->
            $"""
    <add sel="/defaults">
      <dataset macro="{macro}">
        <properties>
          <resourceareas>
{areaLines}
          </resourceareas>
        </properties>
      </dataset>
    </add>
    """

    XElement.Parse(xml)

// The canonically cased sector macro a grant applies to (every state case carries it).
let private grantSectorMacro (grant: SectorResourceGrant) =
    match grant.State with
    | HasResourceAreas macro -> macro
    | HasProperties(macro, _) -> macro
    | NoDataset macro -> macro

// One retrofit block per sector: look the sector up (skipping it gracefully when the
// player doesn't own that DLC), then create each resource area 'amount' times —
// create_resource_area's yieldname is exactly the mapdefaults <resourcearea> ref.
let private sectorRetrofitXml (grant: SectorResourceGrant) =
    let macro = grantSectorMacro grant

    let creates =
        grant.Areas
        |> List.map (fun (ref, amount) ->
            $"""          <do_all exact="{amount}">
            <create_resource_area sector="$Sector" yieldname="'{ref}'" />
          </do_all>""")
        |> String.concat "\n"

    $"""        <find_sector name="$Sector" macro="macro.{macro}" />
        <do_if value="$Sector.exists">
{creates}
          <debug_text text="'AfterFallMod: granted resource areas in {macro}'" />
        </do_if>"""

// Write the run-once MD script that grants the DLC sector resource areas to EXISTING
// saves. mapdefaults <resourcearea> entries are seeded into a save at game start, so
// saves from before this mod version never receive the DLC grants — this script
// creates them at runtime on the first load instead. Two save-persisted guards stop
// it re-applying: the Retrofit cue completes after firing once, and the versioned
// md.$ flag on the global blackboard survives even a disable/re-enable of the mod.
// New games are covered by mapdefaults, so the GameStart path just sets the flag.
// NOTE for future releases that change resource grants: this flag is stamped with the
// content.xml version. A new version's script must grant only the DELTA to saves that
// carry an older flag (or use <patch sinceversion> on the Retrofit cue) — re-emitting
// the full list under a new flag would duplicate areas in retrofitted saves.
let writeResourceRetrofitMD (grants: SectorResourceGrant list) =
    let modVersion =
        XDocument
            .Load(__SOURCE_DIRECTORY__ + "/mod_xml/content.xml")
            .Root.Attribute(XName.Get "version")
            .Value

    let flag = $"md.$AfterFallMod_DLCResourceRetrofit_v{modVersion}"
    let sectorBlocks = grants |> List.map sectorRetrofitXml |> String.concat "\n"

    let xml =
        $"""<mdscript name="AfterFallMod_ResourceRetrofit" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="md.xsd">
  <cues>
    <!-- New game: mapdefaults already seeded every resource area, so just mark this
         save as granted. md.Setup.GameStart is only signalled at genuine game start,
         never when loading a save. -->
    <cue name="NewGame" namespace="this" version="1">
      <conditions>
        <event_cue_signalled cue="md.Setup.GameStart" />
      </conditions>
      <actions>
        <set_value name="{flag}" exact="1" />
        <cancel_cue cue="Retrofit" />
      </actions>
    </cue>
    <!-- Existing save loaded with this mod version for the first time: create the DLC
         sector resource areas that mapdefaults could not deliver retroactively. Base
         game sector grants are excluded - they already applied at game start in every
         existing modded save. -->
    <cue name="Retrofit" namespace="this" version="1">
      <conditions>
        <event_game_loaded />
      </conditions>
      <actions>
        <do_if value="not {flag}?">
          <set_value name="{flag}" exact="1" />
{sectorBlocks}
        </do_if>
        <cancel_cue cue="NewGame" />
      </actions>
    </cue>
  </cues>
</mdscript>"""

    X4.WriteModfiles.write_xml_file "core" "md/afterfallmod_resource_retrofit.xml" (XElement.Parse(xml))
// DLC sectors included: their datasets are reachable from the core file because the
// game merges all extensions' mapdefaults into one document (see mapDefaultsDocs).
// The file already has a hand written template diff with a couple of access licence
// fixes (mod_xml/libraries/mapdefaults.xml). Program.fs copies the templates into
// the mod directory before we run, and our write below replaces that copy - so load
// the template as the seed document and append to it.
let writeMapDefaults (grants: SectorResourceGrant list) =
    let diff =
        XElement.Load(__SOURCE_DIRECTORY__ + "/mod_xml/libraries/mapdefaults.xml")

    for grant in grants do
        diff.Add(sectorResourceAreasXml grant)

    X4.WriteModfiles.write_xml_file "core" "libraries/mapdefaults.xml" diff
