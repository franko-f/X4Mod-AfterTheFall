// For more information see https://aka.ms/fsharp-console-apps
open System.Xml.Linq
open FSharp.Data
open X4.Data
open X4.Utilities
//open X4.Gates

let cluster = findClusterFromSector "cluster_44_sector001_macro"
printfn "%A" cluster
printfn "%A" (findCluster "Cluster_44_macro")
//exit 0

// Now actually generate the files in the mod directory by spitting out XML or copying
// our templates.
X4.WriteModfiles.copy_templates_to_mod()
//X4.WriteModfiles.copyFileToAllDLCs "libraries/region_definitions.xml" // Testing to see if each DLC needs it's own copy of deinitions. Is that the reason creating DLC resources isn't working?
X4.God.generate_god_file "libraries/god.xml"
X4.Jobs.generate_job_file "libraries/jobs.xml"
X4.Resources.generate_resource_definitions_file ()
X4.Ships.generate_abandoned_ships_file "/md/placedobjects.xml"
