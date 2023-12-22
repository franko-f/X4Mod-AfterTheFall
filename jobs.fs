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
let job_replace_quota_xml (id:string) (galaxy:int) (maxGalaxy: Option<int>) (cluster:Option<int>) (sector:Option<int>) =
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

// Build XML that will replace or add a 'prefer build' tag.
// We want to force many jobs for factions to be built at shipyards, rather
// than get spawned in automatically. Means the factions will start weaker,
// but, given time, will build up their fleets.
// If I can easily select a handful of jobs programatically, it's easier than
// taking every job and splitting it in two halves, one for prefer built,
// the other with default spawn,
let job_update_build_xml (id:string) (preferBuild:bool) (job:X4JobMod.Job)=
    // If a job already has an environment, we want to pull it's existing settlings 
    // and create full replace line. Otherwise create a new setting as an 'add' line
    // instead of 'replace' line.
    (*
    match job.Environment with
    | None ->
        // Just build an entire 'add' like for the option.
        None
    | Some environment ->
        // extract excisting settings so we can replace them.
        // gernerate an array of our settings. If it already
        // prefers build, do nothing.
        let settings = [
            yield new XAttribute("preferbuilding", preferBuild)
        ]
        match environment.Buildatshipyard, environment.Preferbuilding with
        | None, None -> None

    let xml = new XElement("replace",
        new XAttribute("sel", $"//jobs/job[@id='{id}']/preferbuild"),
        new XElement("preferbuild", preferBuild)
    )
    printfn "  REPLACING JOB PREFERBUILD %s with \n %s" id (xml.ToString())
    xml
    *)
    None 


// Each job has a faction that it belongs to. The fields in the XML seem a bit unreliable
// USUALLY it seems to be dictated by the category.faction, but this doesn't always exist.
// If not, best guess seems to be look at the ship specified, and last of all, which factions
// territory the job will be located in. (a job can specify several, for pirates, for example)
let getJobFaction (job:X4Job.Job) =
    // Category should give us the faction
    match job.Category with
    | Some category -> Some category.Faction
    | None ->
        match job.Ship with
        | Some ship -> Some ship.Select.Faction 
        | None ->
            printfn "  NO CATEGORY/FACTION FOR JOB %s - defaulting to location.faction" job.Id; 
            // Location is really more about which factional territory the job is in.
            match job.Location.Faction with
            | None -> printfn "  NO FACTION FOR JOB %s" job.Id; None
            | Some faction -> Some faction

// kind of the opposite of getJobFaction - We discover in which factions sectors
// this job is allowed. I assume it's associated with Galaxy class
let getJobLocationFaction (job:X4Job.Job) =
    match job.Location.Faction with
    | None ->
        // if location faction is not specified, then we assume it's the same as 'category'
        match job.Category with
        | None ->  printfn "  NO FACTION FOR JOB %s" job.Id; None
        | Some category -> Some category.Faction
    | Some faction -> Some faction

let getJobFactionName (job:X4Job.Job) =
    getJobFaction job |> Option.defaultValue "NONE"

// Checks if it's a police job, or lacks a faction, or something else
// that makes it sa non standard faction job that were not interested in.
// Returns None if we should ignore it, or Some FactionName
let isStandardFactionJob (job:X4Job.Job) =
    let trafficJob = match job.Task with | None -> false | Some task -> task.Task = "masstraffic.generic" || task.Task = "masstraffic.police"
    match trafficJob with 
    | true -> None // Traffic jobs are special: station mass traffic.
    | false -> getJobFaction job

let getJobTags (job:X4Job.Job) =
    match job.Category with
    | None -> []
    | Some category ->Utilities.parseStringList category.Tags


let processJob (job:X4Job.Job) =
        
    // THINGS TO CHECK:
    // 1. is job.startactive false? Then ignore
    // 2. is category.shipsize 'ship_xl' and tags 'military' or 'resupply' : set to 'preferbuild' 
    // 3. is job.category.faction xenon? Then double quota

    let tags = getJobTags job
    match isStandardFactionJob job with
    | None ->
        printfn "IGNORING JOB %s, tags: %A" job.Id tags
        None
    | Some faction ->
        // Extract data about the job, so we can summarise it.
        // Find out which jobs are outside of the factions sectors:
        let location = 
            match job.Location.Macro with
            | None -> "----"
            | Some location -> location

        let inTerritory =
            match job.Location.Class with
            | "sector" -> if (isFactionInSector faction location) then "Yes" else "No"
            | "zone" -> 
                match findSectorFromZone location allSectors with
                | None -> "???" // Can this happen?
                | Some sector -> if (isFactionInSector faction sector) then "Yes" else "No"
            | "cluster" ->
                if (doesFactionHavePresenceInLocationCluster faction location) then "Yes" else "No"
            | "galaxy" -> "Yes" // well, if the class is galaxy, then definitely.
            | _ -> 
                printfn "  UNHANDLED LOCATION CLASS %s" job.Location.Class
                "???"   // Unhandled location class.
 
        let shipSize = 
            match job.Category with
            | None -> "----"
            | Some category -> Option.defaultValue "----" category.Size

        let quota =
            let quota = job.Quota
            match quota.Galaxy, quota.Maxgalaxy, quota.Cluster, quota.Sector, quota.Wing with
            | None, None, None, None, None -> "----"
            | galaxy, maxGalaxy, cluster, sector, wing ->
                let galaxy = Option.defaultValue 0 galaxy
                let maxGalaxy = Option.defaultValue 0 maxGalaxy
                let cluster = Option.defaultValue 0 cluster
                let sector = Option.defaultValue 0 sector
                let wing = Option.defaultValue 0 wing
                sprintf "%3d/%-3d, %3d, %3d, %3d" galaxy maxGalaxy cluster sector wing

        printfn "PROCESSING JOB %52s, %20s/%-20s: InTerritory:%3s, class:%8s, location:%30s. size:%8s : %s %A" 
             job.Id (getJobFactionName job) (getJobLocationFaction job |> Option.defaultValue "NONE") inTerritory job.Location.Class location shipSize quota tags
        None
    // TODO = now what do we do with faction?
    // Some (job_replace_xml job.Id 42 (Some 42) (Some 3) (Some 1)) // test
//    None 



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

    // lets find out some interesting things about jobs:
    let categoryTags =
        allJobs |> List.fold (
                        fun (tags:Map<string list, int>) (job:X4Job.Job) -> 
                            let jobtags = getJobTags job
                            match Map.tryFind jobtags tags with
                            | None -> Map.add jobtags 1 tags
                            | Some count -> Map.add jobtags (count + 1) tags
                    ) Map.empty
    // write out the tags and counts
    printfn "JOB CATEGORIES:"
    categoryTags |> Map.iter (fun tag count -> printfn "  %A: %d" tag count)


    let jobCount = allJobs.Length

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
  