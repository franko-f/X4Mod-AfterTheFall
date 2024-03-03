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


// different DLC are referred by names such as 'split', 'terran', 'pirate', 'boron'
// This maps that code to the output directory in the mod for that DLC
let DLCOutDirectory = Map [
    "core", modOutDirectory;
    "split", modOutDirectory + "/extensions/ego_dlc_split";
    "terran", modOutDirectory + "/extensions/ego_dlc__terran";
    "pirate", modOutDirectory + "/extensions/ego_dlc_pirate";
    "boron", modOutDirectory + "/extensions/ego_dlc_boron";
]
let GetDLCOutDirectory (dlc:string) = DLCOutDirectory.[dlc]

// Write our XML output to a directory called 'mod'. If the directrory doesn't exist, create it.
let write_xml_file (dlc:string) (filename:string) (xml:XElement) =
    let fullname = (GetDLCOutDirectory dlc) + "/" + filename
    check_and_create_dir fullname   // filename may contain parent folder, so we use fullname when checking/creating dirs.
    xml.Save(fullname)

let copy_templates_to_mod () =
    directoryCopy (__SOURCE_DIRECTORY__ + "/mod_xml") modOutDirectory true
    printfn "Templates copied to mod directory."