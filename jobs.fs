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
open X4.Utilities


let X4JobFileCore = X4UnpackedDataFolder + "/core/libraries/jobs.xml"
let X4JobFileTerran = X4UnpackedDataFolder + "/terran/libraries/jobs.xml"
let X4JobFilePirate = X4UnpackedDataFolder + "/pirate/libraries/jobs.xml"
[<Literal>] // split will be the template for normal job files, as the core game file doesn't use some tags (eg, 'preferbuilding')
let X4JobFileSplit = X4UnpackedDataFolder + "/split/libraries/jobs.xml"
[<Literal>] // We're going to use the boron as a template for diff/Add Job format file. Not all DLC do this.
let X4JobFileBoron = X4UnpackedDataFolder + "/boron/libraries/jobs.xml"

// two job file formats to parse:
// One normal game XML file, and the other is a DIFF file with jobs inside an 'add' selector tag.
type X4Job = XmlProvider<X4JobFileSplit>
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
let replaceQuotaXml (id:string) (galaxy:int) (maxGalaxy: Option<int>) (cluster:Option<int>) (sector:Option<int>) =
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
let updateBuildXml (id:string) (preferBuild:bool) (job:X4JobMod.Job)=
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
// If not, best guess seems to be look at the ship specified in the job, and use it's faction.
let getFaction (job:X4Job.Job) =
    // Category should give us the faction
    match job.Category with
    | Some category -> Some category.Faction
    | None ->
        match job.Ship with
        | Some ship -> Some ship.Select.Faction 
        | None ->
            // According to the debug dump, this never actually happens, but we'll Leave
            // the code here in case it does in a future DLC
            Some "NONE"

// kind of the opposite of getJobFaction - We discover in which factions sectors
// this job is allowed. I assume it's associated with Galaxy class
let getLocationFaction (job:X4Job.Job) =
    match job.Location.Faction with
    | None ->
        // if location faction is not specified, then we assume it's the same as 'category'
        match job.Category with
        | None ->  printfn "  NO FACTION FOR JOB %s" job.Id; None
        | Some category -> Some category.Faction
    | Some faction -> Some faction

let getFactionName (job:X4Job.Job) =
    getFaction job |> Option.defaultValue "NONE"


// IT looks like the 'faction' in a job location does not mean 'put it in this
// factions territory', but instead also uses two other fields: 'relation' and
// 'comparison' to determine exactly where the job can be spawned.
// Basically, you compare the 'faction' name to 'relation' using 'comparison'
// operator.
// eg 'xenon' 'self' 'lt' seems to mean 'spawn in any territory less that is
// less than 'xenon': ie, all factions except xenon.
// faction="teladi" relation="ally" comparison="ge"  - any ally  (does 'ge'
// mean ally or my own territory?)
// faction="[teladi, ministry]" relation="self" comparison="exact" spawn
// only in teladi or ministry sectors.
// I see this sometimes:
//    xenon (ge) ally
// I'm guessing this means xenon or neutral sectors.
// This function extracts that data for writing an informational line
let getLocationFactionRelation (job:X4Job.Job) =
    let faction = getLocationFaction job |> Option.defaultValue "NONE"
    match job.Location.Comparison, job.Location.Relation with
    | None, None -> sprintf "%s" faction
    | Some comparison, Some relation -> sprintf "%s (%s) %s" faction comparison relation
    | _ -> sprintf "%s" faction     // This case never happens in the current data as of DLC 4:Boron

// Checks if it's a police job, or lacks a faction, or something else
// that makes it sa non standard faction job that were not interested in.
// Returns None if we should ignore it, or Some FactionName
let isMinorTask (job:X4Job.Job) =
    match job.Task with | None -> false | Some task -> task.Task = "masstraffic.generic" || task.Task = "masstraffic.police"

let getJobTags (job:X4Job.Job) =
    match job.Category with
    | None -> []
    | Some category -> Utilities.parseStringList category.Tags


// Subordinate jobs are things like escorts or 'subordinate' ships.
// They're wings of fighters on carriers, etc. We want to ignore these,
// as it would be easu to overtune the xenon by accidentally exponentially
// increasing the number of ships in a fleet.
let isSubordinate (job:X4Job.Job) =
    match job.Modifiers with
    | None -> false
    | Some modifiers ->
        match modifiers.Subordinate with
        | None -> false
        | Some subordinate -> true

// Does this ship have subordinates? ie, is it a carrier? Destroyer group?
let hasSubordinate (job:X4Job.Job) =
    match job.Subordinates with
    | None -> false
    | Some subordinates -> true

let subordinateIds (job:X4Job.Job) =
    match job.Subordinates with
    | None -> [|""|]
    | Some subordinates ->
        subordinates.Subordinates |> Array.map (fun subordinate -> subordinate.Job)

// Some jobs are flagged to start immediately when the game begins.
// Other jobs only activate on a given trigger. We want to ignore those.
// defaults to true when not set
let isStartActive (job:X4Job.Job) = job.Startactive  |> Option.defaultValue true

let sectorQuota (job:X4Job.Job) = job.Quota.Sector |> Option.defaultValue 0
let galaxyQuota (job:X4Job.Job) = job.Quota.Galaxy |> Option.defaultValue 0
let maxGalaxyQuota (job:X4Job.Job) = job.Quota.Maxgalaxy |> Option.defaultValue 0
let clusterQuota (job:X4Job.Job) = job.Quota.Cluster |> Option.defaultValue 0
let wingQuota (job:X4Job.Job) = job.Quota.Wing |> Option.defaultValue 0

let getTagList (job:X4Job.Job) =
    match job.Category with
    | None -> []
    | Some category -> Utilities.parseStringList category.Tags

let isMilitaryJob (job:X4Job.Job) =
    getTagList job |> List.exists (fun tag -> tag = "military")

// 'preferbuilding' means that the ships won't be autospawned. In theory, they
// will get queued to build at shipyards instead.
let isPreferBuild (job:X4Job.Job) =
    match job.Environment with
    | None -> false
    | Some environment -> environment.Preferbuilding |> Option.defaultValue false

let buildAtShipyard (job:X4Job.Job) =
    match job.Environment with
    | None -> false
    | Some environment -> environment.Buildatshipyard



// Write some useful data about a job to the console.
let printJobInfo (job:X4Job.Job) =
    let tags = getJobTags job |> String.concat ", "
    match isMinorTask job with
    | true ->
        printfn "IGNORING JOB %s, tags: %A" job.Id tags
    | false ->
        let faction = getFaction job |> Option.defaultValue "NONE"  // Will never return "NONE" as we ignore minor tasks.
        // Extract data about the job, so we can summarise it.
        // Find out which jobs are outside of the factions sectors:
        let location = 
            match job.Location.Macro with
            | None -> "----"
            | Some location -> location

        let inTerritory =
            match job.Location.Class with
            | "sector" -> if (isFactionInSector faction location) then "InTerritory" else ""
            | "zone" -> 
                match findSectorFromZone location allSectors with
                | None -> "???" // Can this happen?
                | Some sector -> if (isFactionInSector faction sector) then "InTerritory" else "No"
            | "cluster" ->
                if (doesFactionHavePresenceInLocationCluster faction location) then "InTerritory" else "No"
            | "galaxy" -> "InTerritory" // well, if the class is galaxy, then definitely.
            | _ -> 
                printfn "  UNHANDLED LOCATION CLASS %s" job.Location.Class
                "???"   // Unhandled location class.
 
        let shipSize = 
            match job.Category with
            | None -> "----"
            | Some category -> Option.defaultValue "----" category.Size

        let quota =
            let galaxy    = job |> galaxyQuota
            let maxGalaxy = job |> maxGalaxyQuota
            let sector    = job |> sectorQuota
            let cluster   = job |> clusterQuota
            let wing      = job |> wingQuota
            sprintf "%3d/%-3d, %3d, %3d, %3d" galaxy maxGalaxy cluster sector wing

        let subordinate = isSubordinate job |> either "escort" ""
        let subordinates = hasSubordinate job |> either "escorted" ""
        let subordinatelist = subordinateIds job |> String.concat ", "
        let preferBuild = match isPreferBuild job with false -> "" | true -> "preferbuild"
        let startactive = match isStartActive job with false -> "inactive" | true -> ""

        printfn "PROCESSING JOB %52s, %20s/%-32s: %12s, %8s / %-30s | %8s : %s %8s %6s %8s %11s [%-44s] escorts: %s"
             job.Id (getFactionName job) (getLocationFactionRelation job) inTerritory job.Location.Class location shipSize quota subordinates subordinate startactive preferBuild tags subordinatelist


let processJob (job:X4Job.Job) =
    // THINGS TO CHECK:
    // 1. is job.startactive false? Then ignore
    // 2. is faction non-xenon, category.shipsize 'ship_xl' and tags 'military'
    //    or 'resupply'?
    //    set to 'preferbuild' so that factions don't start with large military
    //    ships at all
    // 3. is job.category.faction xenon? Then increase quota for military jobs,
    //   and increase economy jobs by a different factor. We have to be
    //   carefuly. it will already be bad enough with increased military to
    //   start, and the extra territory and stations, so don't accidentally
    //   turbocharge their economy vs the crippled factions.
    //   Need to consider only small tweaks. Lets try:
    //   XL military jobs: 50% increase. round up
    //   S & M military jobs: 70% increase. round up
    //   Economy: 30% increase. round up
    // 4. Is job subordinate? LEAVE ALONE.
    // 4. Leave the following special plot ships alone:
    //    boron_carrier_patrol_xl_flagship
    printJobInfo job

    None
    // TODO = now what do we do with faction?
    // Some (job_replace_xml job.Id 42 (Some 42) (Some 3) (Some 1)) // test



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
  