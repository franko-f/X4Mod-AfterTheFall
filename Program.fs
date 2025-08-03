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
//printfn "\nAll Ship Equipment:"
//X4.Data.allShipEquipment |> Seq.iter (fun equipment -> printfn $"%-35s{equipment.Name} '{equipment.Class}'")

dumpAllEquipment() |> ignore

printfn "\nFind Asset Tests:"
X4.Data.allShipEquipment
    |> X4.Data.findMatchingAsset "turret" ["boron"; "large"]
    |> List.map (fun asset ->dumpEquipment asset)
    |> ignore

X4.Data.allShipEquipment
    |> X4.Data.findMatchingAsset "turret" ["standard"; "large"]
    |> List.map (fun asset ->dumpEquipment asset)
    |> ignore


X4.Data.findShipByName "ship_bor_l_destroyer_01" |> Option.iter (fun s -> X4.Data.printShipInfo s)
X4.Data.findShipByName "ship_bor_l_miner_solid_01" |> Option.iter (fun s -> X4.Data.printShipInfo s)
X4.Data.findShipByName "ship_bor_m_miner_solid_01" |> Option.iter (fun s -> X4.Data.printShipInfo s)
X4.Data.findShipByName "ship_tel_l_miner_solid_01" |> Option.iter (fun s -> X4.Data.printShipInfo s)
X4.Data.findShipByName "ship_tel_m_miner_solid_01" |> Option.iter (fun s -> X4.Data.printShipInfo s)
X4.Data.findShipByName "ship_atf_l_destroyer_01" |> Option.iter (fun s -> X4.Data.printShipInfo s)
X4.Data.findShipByName "ship_atf_xl_battleship_01" |> Option.iter (fun s -> X4.Data.printShipInfo s)
X4.Data.findShipByName "ship_arg_l_destroyer_01" |> Option.iter (fun s -> X4.Data.printShipInfo s)

X4.Data.dumpShips()

exit 0

// Now actually generate the files in the mod directory by spitting out XML or copying
// our templates.
X4.WriteModfiles.copy_templates_to_mod()

X4.God.generate_god_file "libraries/god.xml"
X4.Jobs.generate_job_file "libraries/jobs.xml"
X4.Resources.generate_resource_definitions_file ()
X4.Ships.generate_abandoned_ships_file "/md/placedobjects.xml"

X4.ProductQuotaInfo.printTable()
