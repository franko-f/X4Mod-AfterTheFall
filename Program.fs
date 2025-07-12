// For more information see https://aka.ms/fsharp-console-apps
open System.Xml.Linq
open FSharp.Data
open X4.Data
open X4.Utilities
//open X4.Gates

//let cluster = findClusterFromSector "cluster_44_sector001_macro"
//printfn "%A" cluster
//printfn "%A" (findCluster "Cluster_44_macro")

//X4.Data.allShips |> Seq.iter (fun ship -> printfn $"%-35s{ship.Name} {ship.Size}")
printfn "\nAll Ship Equipment:"
X4.Data.allShipEquipment |> Seq.iter (fun equipment -> printfn $"%-35s{equipment.Name} '{equipment.Class}'")
printfn "\nFind Asset Tests:"
X4.Data.allShipEquipment
    |> X4.Data.findMatchingAsset "turret" "boron large"
    |> List.map (fun asset ->
            // iterate through the connections to find connections with tags.
            printfn $"%-35s{asset.Name} '{asset.Class}'"
            asset.Connections
                |> Array.map (fun connection -> printfn "   %s" connection.Tags)
            // |> Seq.iter (fun connection ->
            //     printfn "   %s" (connection.Tags |> String.concat ", "))
            // asset
        )
    |> ignore

//exit 0

// Now actually generate the files in the mod directory by spitting out XML or copying
// our templates.
X4.WriteModfiles.copy_templates_to_mod()

X4.God.generate_god_file "libraries/god.xml"
X4.Jobs.generate_job_file "libraries/jobs.xml"
X4.Resources.generate_resource_definitions_file ()
X4.Ships.generate_abandoned_ships_file "/md/placedobjects.xml"

X4.ProductQuotaInfo.printTable()
