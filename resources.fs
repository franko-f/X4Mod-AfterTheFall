/// Responsible for adding resources to select sectors for each faction.
/// We add just a few resources as required for the factions to have a basic working economy in
/// the sectors they have been moved to.
///
/// As of X4 9.0 this is a two part job, driven by the same per-sector assignments:
/// 1. VISUALS: place our custom region definitions (region_definitions.xml) as CLUSTER
///    connections in the clusters file. Regions use global positioning rather than
///    referring to the sector directly. These provide the physical asteroid/gas fields.
/// 2. MINABLES: add <resourcearea> entries to each sector's dataset in
///    libraries/mapdefaults.xml. These are invisible yield volumes that roam within the
///    sector's regions and fill their fields with minable resources. Without them the
///    asteroids placed in step 1 are empty; without step 1 they have nothing to fill.

module X4.Resources

open System.Xml.Linq
open X4.Tuning.Resources // resourceMap, resourceAreaMap and field placement offsets
open X4.Territories
open X4.Data
open System
open System.Xml


(*
Example region definition. It's a connection in the clusters.xml file.

    <connection name="C01S01_Region004_connection" ref="regions">
    <offset>
        <position x="106698.3515625" y="0" z="-73481.3515625" />
    </offset>
    <macro name="C01S01_Region004_macro">
        <component connection="cluster" ref="standardregion" />
        <properties>
        <region ref="p1_40km_hydrogen_field" />
        </properties>
    </macro>
    </connection>

We need to add it to the cluster macro, in it's connections list in clusters.xml

<macros>
  <macro name="Cluster_01_macro" class="cluster">
    <component ref="standardcluster" />
    <connections>
        .... [ the connection above]
    </connections>
  </macro>

So this would be a diff file with an ADD operation, with the selector being the /macros/macro[name='Cluster_01_macro']/connections node.

*)

let rand = new Random(12345) // Seed the random number generator so we get the same results each time, as long as we're not adding new regions or changing territory order.

let processRegion cluster sector resource (count: int) =
    // randomly place the region in the sector, offseting it between -80km to +80 in the x,z coordinates, and up to 5 km in the y coordinate.
    let x, y, z = getSectorPosition sector

    let x, y, z =
        x + rand.Next(-FieldPlacementOffsetXZ, FieldPlacementOffsetXZ),
        y + rand.Next(-FieldPlacementOffsetY, FieldPlacementOffsetY),
        z + rand.Next(-FieldPlacementOffsetXZ, FieldPlacementOffsetXZ)

    let region = resourceMap.[resource]
    let regionName = $"{sector}_region_{resource}_{count}" //%s_%s_Region00" cluster sector
    printfn "%s     Cluster: %s:%s,  Resource: %s:%s @ %A" regionName cluster sector resource region (x, y, z)

    // Generate and return the ADD XML for the region

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
    // Using the textreader instead of XElement.Parse preserves whitespace and carriage returns in our output.
    let xtr = new XmlTextReader(new System.IO.StringReader(xml))
    XElement.Load(xtr)


// Turn a list into an infinite sequence that cycles through the list from the start once you reach the end
let cycle (data: string list) =
    Seq.initInfinite (fun i -> data.[i % data.Length])

// Compute the (cluster, sector, resource) assignments for a DLC. This is the single
// source of truth shared by the visual region diffs (clusters file) and the 9.0
// mapdefaults resource area diffs, so the visible fields and the minable resource
// areas always land in the same sectors.
let computeResourceAssignments (dlc: string) = [
    for territory in (X4.Data.dlcTerritories dlc) do
        // extract each cluster and list of resource definition from territories, then look up the sectors in the cluster
        // 'sectors' is an infinite sequence that will start again at the first sector in the cluster when it reaches the end
        let sectors =
            getFactionSectorsInCluster territory.faction territory.cluster |> cycle
        // iterate through the resources, and cycle through the sectors in the cluster,
        // assigning each resource to a sector in a round robin fashion
        yield!
            Seq.zip sectors territory.resources
            |> Seq.map (fun (sector, resource) -> (territory.cluster, sector, resource))
            |> Seq.toList
]

// Generate the visual mining field regions: one cluster connection per assignment,
// placed at a seeded-random offset around the sector centre. IMPORTANT: this loop is
// the only consumer of 'rand' above; keep the call sequence stable so region positions
// don't churn between runs.
let generateDLCVisualRegions (dlc: string) (filename: string) assignments =
    printfn "======= Generating visual mining regions for DLC: %s" dlc

    let mutable counter = 0

    let resourceDiff = [
        for (cluster, sector, resource) in assignments do
            counter <- counter + 1
            processRegion cluster sector resource counter
    ]

    // Create the new XML Diff document to contain our region additions
    let diff =
        XElement.Parse(
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>
        <diff>
        </diff>
        "
        )

    // Now add the region changes, one by one, to the the xml diff.
    [|
        for element in resourceDiff do
            diff.Add(element)
            diff.Add(new XText("\n")) // Add a newline after each element so the output is readible
    |]
    |> ignore

    WriteModfiles.write_xml_file dlc filename diff


// ==== 9.0 mapdefaults resource areas ====

// Merge the resource area lists of all the resources assigned to one sector, summing
// the amounts of identical area kinds. Needed because the round robin wraps around:
// a single sector cluster assigned 'minerals' twice should get double the areas in
// one <add> operation, not two competing operations.
let mergeResourceAreas (resources: string list) =
    resources
    |> List.collect (fun resource -> resourceAreaMap.[resource])
    |> List.groupBy (fun area -> { area with amount = 0 })
    |> List.map (fun (kind, areas) -> { kind with amount = areas |> List.sumBy (fun a -> a.amount) })

// Generate the diff <add> operation granting a sector its resource areas. The shape of
// the operation depends on how the sector appears in the vanilla mapdefaults file:
// an existing <resourceareas> node gets children appended; a dataset without one gets
// the whole node; a sector with no dataset at all (rare, e.g. Cluster_714) gets a
// complete new dataset added to the document root.
let processSectorResourceAreas (dlc: string) (sector: string) (resources: string list) =
    let areaLines =
        mergeResourceAreas resources
        |> List.map (fun area ->
            let ref = X4.Data.makeResourceAreaRef area.size area.ware area.yieldTier area.speed
            $"""        <resourcearea amount="{area.amount}" ref="{ref}" />""")
        |> String.concat "\n"

    let xml =
        match X4.Data.getMapDefaultsDatasetState dlc sector with
        | X4.Data.HasResourceAreas macro ->
            $"""
    <add sel="/defaults/dataset[@macro='{macro}']/properties/resourceareas">
{areaLines}
    </add>
    """
        | X4.Data.HasProperties(macro, Some anchor) ->
            // insert after the anchor child to respect the schema's element order
            $"""
    <add sel="/defaults/dataset[@macro='{macro}']/properties/{anchor}" pos="after">
      <resourceareas>
{areaLines}
      </resourceareas>
    </add>
    """
        | X4.Data.HasProperties(macro, None) ->
            $"""
    <add sel="/defaults/dataset[@macro='{macro}']/properties" pos="prepend">
      <resourceareas>
{areaLines}
      </resourceareas>
    </add>
    """
        | X4.Data.NoDataset macro ->
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

    // Using the textreader instead of XElement.Parse preserves whitespace and carriage returns in our output.
    let xtr = new XmlTextReader(new System.IO.StringReader(xml))
    XElement.Load(xtr)

let generateDLCMapDefaults (dlc: string) assignments =
    printfn "======= Generating mapdefaults resource areas for DLC: %s" dlc

    // One <add> per sector: gather every resource assigned to the sector together,
    // so the NoDataset case can't produce two competing <dataset> nodes.
    let bySector =
        assignments
        |> List.groupBy (fun (_, sector, _) -> sector)
        |> List.map (fun (sector, entries) -> sector, [ for (_, _, resource) in entries -> resource ])

    // The core game file already has a hand written template diff with a couple of
    // access licence fixes (mod_xml/libraries/mapdefaults.xml). Program.fs copies the
    // templates into the mod directory before we run, and our write below replaces
    // that copy - so load the template as the seed document and append to it. The
    // DLC files have no template and start from an empty diff.
    let diff =
        match dlc with
        | "core" ->
            let xtr = new XmlTextReader(__SOURCE_DIRECTORY__ + "/mod_xml/libraries/mapdefaults.xml")
            XElement.Load(xtr)
        | _ ->
            XElement.Parse(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>
        <diff>
        </diff>
        "
            )

    for (sector, resources) in bySector do
        diff.Add(processSectorResourceAreas dlc sector resources)
        diff.Add(new XText("\n"))

    WriteModfiles.write_xml_file dlc "libraries/mapdefaults.xml" diff


let generate_resource_definitions_file () =
    // Each DLC has a completely different naming scheme for the cluster file.
    // The file *must* be named the same as the dlc, otherwise the patching will fail.
    let dlcClusterFiles = [
        "core", "maps/xu_ep2_universe/clusters.xml"
        "terran", "maps/xu_ep2_universe/dlc_terran_clusters.xml"
        "boron", "maps/xu_ep2_universe/dlc_boron_clusters.xml"
        "split", "maps/xu_ep2_universe/dlc4_clusters.xml"
        "pirate", "maps/xu_ep2_universe/dlc_pirate_clusters.xml"
    ]

    for (dlc, clusterFile) in dlcClusterFiles do
        let assignments = computeResourceAssignments dlc
        generateDLCVisualRegions dlc clusterFile assignments // the physical asteroid/gas fields
        generateDLCMapDefaults dlc assignments // the minable yields that fill them
