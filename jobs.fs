/// Parse the X4 jobs file from the core game and the DLCs.
/// Process job quotas, reducing AI faction ships and fleets, but increase the
/// Xenons ships and fleets.
/// We try to only impact the *initial* job quotas, so that the AI factions can
/// still build up their fleets using their shipyards and wharfs. The intent is
/// to reduce strength at game start vs the xenon, not permanently cripple them.

module X4.Jobs

open System.Xml.Linq
open FSharp.Data
//open X4MLParser.Utilities
open X4.Data


[<Literal>]
let X4JobFileCore = X4UnpackedDataFolder + "/core/libraries/jobs.xml"
let X4JobFileSplit = X4UnpackedDataFolder + "/split/libraries/jobs.xml"
let X4JobFileTerran = X4UnpackedDataFolder + "/terran/libraries/jobs.xml"
let X4JobFilePirate = X4UnpackedDataFolder + "/pirate/libraries/jobs.xml"
[<Literal>] // We're going to use the boron as a template for diff/Add Job format file. Not all DLC do this.
let X4JobFileBoron = X4UnpackedDataFolder + "/boron/libraries/jobs.xml"

type X4Job = XmlProvider<X4JobFileCore>
type X4JobMod = XmlProvider<X4JobFileBoron>  // Use this as a sample file so we can parse the DLC jobs files that use the DIFF format.

// This string is the starting point for the output job we'll write.
let X4JobModTemplate = "<?xml version=\"1.0\" encoding=\"utf-8\"?>
        <diff>
            <add sel=\"/jobs\">
            </add>
        </diff>
    "



let getJobsFromDiff (diff:X4JobMod.Add[]) = 
    let jobsAdd = Array.filter (fun (add:X4JobMod.Add) -> add.Sel = "/jobs") diff
    [|  for jobs in jobsAdd do
            for job in jobs.Jobs do
                yield new X4Job.Job(job.XElement)
    |]


// Construct an XML element representing a 'replace' tag that will replace the quotas for a given job.
// example replace line:
// <replace sel="/jobs/job[@id='xen_energycells']/@quotas">
//   <quotas>
//      <quota galaxy="42" cluster="3"/>
//   </quotas>
// </replace>
let job_replace_xml (id:string) (galaxy:int) (maxGalaxy: Option<int>) (cluster:Option<int>) (sector:Option<int>) =
    let quotas = [
        yield new XAttribute("galaxy", galaxy)
        match maxGalaxy with Some x -> yield new XAttribute("maxgalaxy", x) | _ -> ()
        match cluster with Some x -> yield new XAttribute("cluster", x) | _ -> ()
        match sector with Some x -> yield new XAttribute("sector", x) | _ -> ()
    ]

    let xml = new XElement("replace",
        new XAttribute("sel", $"//jobs/job[@id='{id}']/quotas"),
        new XElement("quota", quotas)
    )
    printfn "  REPLACING JOB QUOTA %s with \n %s" id (xml.ToString())
    xml 


let processJob (job:X4Job.Job) =
    printfn "PROCESSING JOB %s" job.Id
    let trafficJob = match job.Task with | None -> false | Some task -> task.Task = "masstraffic.generic" || task.Task = "masstraffic.police"

    match trafficJob with 
    | true -> None // Traffic jobs are special: station mass traffic.
    | false ->
        let faction = match job.Location.Faction with
                        | None -> 
                            match job.Category with
                            | None ->
                                printfn "  NO FACTION FOR JOB %s" job.Id; None
                            | Some category ->
                                Some category.Faction
                        | Some faction ->
                            Some faction
        // TODO = now what do we do with faction?
        // Some (job_replace_xml job.Id 42 (Some 42) (Some 3) (Some 1)) // test
        None 


// Kick off the work of generating the job file for the mod, and write out the
// XML diff to the given filename
let generate_job_file (filename:string) =
    let X4JobsCore = X4Job.Load(X4JobFileCore)
    let X4JobsSplit = X4Job.Load(X4JobFileSplit)     // Split don't use a diff file.
    let X4JobsPirate = X4Job.Load(X4JobFilePirate)  // same for pirate.
    let X4JobsBoron = X4JobMod.Load(X4JobFileBoron) 
    let X4JobsTerran = X4JobMod.Load(X4JobFileTerran)
    let allJobs = Array.toList <| Array.concat [
                        X4JobsCore.Jobs;
                        X4JobsSplit.Jobs;
                        X4JobsPirate.Jobs;
                        getJobsFromDiff X4JobsTerran.Adds;
                        getJobsFromDiff X4JobsBoron.Adds;
                    ]

    let replaceJobs = [|
        for job in allJobs do
            match processJob job with Some job -> yield job | _ -> ()
    |]


    let outJobFile = X4JobMod.Parse(X4JobModTemplate)
        // Add out 'remove' tags to the end of the diff block.
    let diff = outJobFile.XElement // the root element is actually the 'diff' tag.
    let changes = Array.concat [replaceJobs]
    [| for element in changes do 
        diff.Add(element)
        diff.Add( new XText("\n")) // Add a newline after each element so the output is readible
    |] |> ignore

    X4.Utilities.write_xml_file filename outJobFile.XElement
  