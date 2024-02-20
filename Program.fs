// For more information see https://aka.ms/fsharp-console-apps
open System.Xml.Linq
open FSharp.Data
open X4.Data
open X4.Utilities
//open X4.Gates


// Now actually generate the files in the mod directory by spitting out XML or copying
// our templates.
directoryCopy (__SOURCE_DIRECTORY__ + "/mod_xml") (__SOURCE_DIRECTORY__ + "/mod/after_the_fall") true
X4.God.generate_god_file "libraries/god.xml"
X4.Jobs.generate_job_file "libraries/jobs.xml"

//X4.Gates.printGatesInZones()
//X4.Gates.printGateConnections()
