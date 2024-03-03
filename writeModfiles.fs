module X4.WriteModfiles

open System.Xml.Linq
open X4.Utilities

let modOutDirectory = __SOURCE_DIRECTORY__ + "/mod/after_the_fall"

// Some factions are part of the core game, some are part of the DLC. 
// Most of the time it's not important, but for some changes, like clusters, we need to be able to
// wrrite the output to the DLC specific extensions dir for it to work. This helps us discover which
// factions are part of which DLC when it comes to writing the output files in cases that it matters
let factionToDLCMap = Map [
    "argon", "core";
    "hatikvah", "core";
    "antigone", "core";
    "teladi", "core";
    "ministry", "core";
    "paranid", "core";
    "alliance", "core";
    "holyorder", "core";
    "split", "split";
    "freesplit", "split";
    "terran", "terran";
    "pioneers", "terran";
    "scavenger", "pirate";
    "loanshark", "pirate";
    "boron", "boron";
]

// find the DLC that this faction is part of.
let factionDLC faction = factionToDLCMap.[faction]
// find the factions that are part of this DLC
let dlcFactions dlc = factionToDLCMap |> Map.filter (fun k v -> v = dlc) |> Map.keys |> List.ofSeq
// A list of all the DLC codes. eg, core, split, etc.
let dlcs = factionToDLCMap |> Map.values |> List.ofSeq |> List.distinct


// different DLC are referred by names such as 'split', 'terran', 'pirate', 'boron'
// This maps that code to the output directory in the mod for that DLC.
// Technically, this really isn't the map of DLCs, as it contains the core game as well, 
// but hey, what you gonna do?
let DLCs = Map [
    "core", modOutDirectory;
    "split", modOutDirectory + "/extensions/ego_dlc_split";
    "terran", modOutDirectory + "/extensions/ego_dlc_terran";
    "pirate", modOutDirectory + "/extensions/ego_dlc_pirate";
    "boron", modOutDirectory + "/extensions/ego_dlc_boron";
]
let getDLCOutDirectory (dlc:string) = DLCs.[dlc]

let getDLCFileName dlc filename = (getDLCOutDirectory dlc) + "/" + filename

// Write our XML output to a directory called 'mod'. If the directrory doesn't exist, create it.
let write_xml_file (dlc:string) (filename:string) (xml:XElement) =
    let fullname = (getDLCFileName dlc filename)
    check_and_create_dir fullname
    xml.Save(fullname)

let copy_templates_to_mod () =
    directoryCopy (__SOURCE_DIRECTORY__ + "/mod_xml") modOutDirectory true
    printfn "Templates copied to mod directory."

let copyFileToDlc dlc (filename:string) =
    let fullname = (getDLCFileName dlc filename)
    check_and_create_dir fullname
    System.IO.File.Copy(__SOURCE_DIRECTORY__ + "/mod_xml/" + filename, fullname, true) |> ignore
    printfn "Copied %s to mod directory." filename

let copyFileToAllDLCs (filename:string) =
    for dlc in dlcs do
        copyFileToDlc dlc filename
        printfn "Copied %s to mod directory." filename