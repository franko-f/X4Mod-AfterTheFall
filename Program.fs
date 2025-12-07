// For more information see https://aka.ms/fsharp-console-apps
open X4.Data
open X4.Ships

X4.Ships.dumpAllShipEquipment () |> ignore

printfn "\nFind Asset Tests:"

X4.Ships.allShipEquipment
|> X4.Ships.findMatchingEquipment (set [ "turret"; "boron"; "large" ])
|> List.map (fun asset -> dumpEquipmentInfo "" asset)
|> ignore

X4.Ships.allShipEquipment
|> X4.Ships.findMatchingEquipment (set [ "shield"; "boron" ])
|> List.map (fun asset -> dumpEquipmentInfo "" asset)
|> ignore

X4.Ships.allShipEquipment
|> X4.Ships.findMatchingEquipment (set [ "shield"; "engine" ])
|> List.map (fun asset -> dumpEquipmentInfo "" asset)
|> ignore

X4.Ships.findShipByName "ship_bor_l_destroyer_01"
|> Option.iter (fun s -> X4.Ships.printShipInfo s)

X4.Ships.findShipByName "ship_bor_l_miner_solid_01"
|> Option.iter (fun s -> X4.Ships.printShipInfo s)

X4.Ships.findShipByName "ship_bor_m_miner_solid_01"
|> Option.iter (fun s -> X4.Ships.printShipInfo s)

X4.Ships.findShipByName "ship_tel_l_miner_solid_01"
|> Option.iter (fun s -> X4.Ships.printShipInfo s)

X4.Ships.findShipByName "ship_tel_m_miner_solid_01"
|> Option.iter (fun s -> X4.Ships.printShipInfo s)

X4.Ships.findShipByName "ship_ter_m_corvette_01"
|> Option.iter (fun s -> X4.Ships.printShipInfo s)

X4.Ships.findShipByName "ship_atf_l_destroyer_01"
|> Option.iter (fun s -> X4.Ships.printShipInfo s)

X4.Ships.findShipByName "ship_atf_xl_battleship_01"
|> Option.iter (fun s -> X4.Ships.printShipInfo s)

X4.Ships.findShipByName "ship_arg_l_destroyer_01"
|> Option.iter (fun s -> X4.Ships.printShipInfo s)

X4.Ships.findShipByName "ship_ter_s_fighter_01"
|> Option.iter (fun s -> X4.Ships.printShipInfo s)

X4.Ships.findShipByName "ship_ter_s_fighter_03"
|> Option.iter (fun s -> X4.Ships.printShipInfo s)

X4.Ships.findShipByName "ship_ter_s_heavyfighter_01"
|> Option.iter (fun s -> X4.Ships.printShipInfo s)

X4.Ships.findShipByName "ship_ter_s_fighter_04"
|> Option.iter (fun s -> X4.Ships.printShipInfo s)

X4.Ships.findShipByName "ship_bor_s_heavyfighter_01"
|> Option.iter (fun s -> X4.Ships.printShipInfo s)

X4.Ships.findShipByName "ship_bor_m_corvette_01"
|> Option.iter (fun s -> X4.Ships.printShipInfo s)

X4.Ships.findShipByName "ship_bor_l_destroyer_01"
|> Option.iter (fun s -> X4.Ships.printShipInfo s)

X4.Ships.findShipByName "ship_bor_s_miner_solid_01"
|> Option.iter (fun s -> X4.Ships.printShipInfo s)

X4.Ships.findShipByName "ship_atf_xl_battleship_01"
|> Option.iter (fun s -> X4.Ships.printShipInfo s)

X4.Ships.generate_abandoned_ships_file "/md/placedobjects.xml"
X4.Ships.generate_abandoned_ships_file "/md/placedobjects.xml"

exit 0

// Now actually generate the files in the mod directory by spitting out XML or copying
// our templates.
X4.WriteModfiles.copy_templates_to_mod ()

X4.God.generate_god_file "libraries/god.xml"
X4.Jobs.generate_job_file "libraries/jobs.xml"
X4.Resources.generate_resource_definitions_file ()

X4.ProductQuotaInfo.printTable ()
