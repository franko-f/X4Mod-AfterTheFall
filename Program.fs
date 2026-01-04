// For more information see https://aka.ms/fsharp-console-apps
open X4.Data
open X4.Ships

X4.Ships.dumpAllShipEquipment () |> ignore

// Now actually generate the files in the mod directory by spitting out XML or copying
// our templates.
X4.WriteModfiles.clean_mod_directory ()
X4.WriteModfiles.copy_templates_to_mod ()

X4.God.generate_god_file "libraries/god.xml"
X4.Jobs.generate_job_file "libraries/jobs.xml"
X4.Resources.generate_resource_definitions_file ()
X4.Ships.generate_abandoned_ships_file "/md/placedobjects.xml" "/libraries/loadouts.xml"

X4.ProductQuotaInfo.printTable ()
