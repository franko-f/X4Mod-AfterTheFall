/// Responsible for adding resources to regions for each faction to select sectors.
/// We add just a few resources as required for the factions to have a basic working economy in
/// the sectors they have been moved to.
/// To assist with this, we've added new region definitions in a region_definitions.xml file.
/// We'll use a combination of existing regions and the new region definitions when assigning resources to sectors.
///
/// To assign a region to a sector, we add a CLUSTER connection to the clusters file. Regions use global positioning,
/// rather than referring to the sector directly.

module X4.Resources

open System.Xml.Linq
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
        x + rand.Next(-80000, 80000), y + rand.Next(-5000, 5000), z + rand.Next(-80000, 80000)

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

let generateDLCResources (dlc: string) (filename: string) =
    printfn "======= Generating resources for DLC: %s" dlc

    // allocate resources to specific sectors.
    let resource_definitions = [
        for territory in (X4.Data.dlcTerritories dlc) do
            // extract each cluster and list of resource definition from territories, then look up the sectors in the cluster
            // 'sectors' is an infinite sequence that will start again at the first sector in the cluster when it reaches the end
            let sectors = findSectorsInCluster territory.cluster |> cycle
            // iterate through the resources, and cycle through the sectors in the cluster,
            // assigning each resource to a sector in a round robin fashion
            yield!
                Seq.zip sectors territory.resources
                |> Seq.map (fun (sector, resource) -> (territory.cluster, sector, resource))
                |> Seq.toList
    ]

    let mutable counter = 0

    let resourceDiff = [
        for (cluster, sector, resource) in resource_definitions do
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


let generate_resource_definitions_file () =
    // Each DLC has a completely different naming scheme for the cluster file.
    // The file *must* be named the same as the dlc, otherwise the patching will fail.
    generateDLCResources "core" "maps/xu_ep2_universe/clusters.xml"
    generateDLCResources "terran" "maps/xu_ep2_universe/dlc_terran_clusters.xml"
    generateDLCResources "boron" "maps/xu_ep2_universe/dlc_boron_clusters.xml"
    generateDLCResources "split" "maps/xu_ep2_universe/dlc4_clusters.xml"
    generateDLCResources "pirate" "maps/xu_ep2_universe/dlc_pirate_clusters.xml"
