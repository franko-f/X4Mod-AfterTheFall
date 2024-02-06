/// Parse the X4 jobs file from the core game and the DLCs.
/// Process job quotas, reducing AI faction ships and fleets, but increase the  Xenons ships and fleets.
/// We try to only impact the *initial* job quotas, so that the AI factions can still build up their
/// fleets using their shipyards and wharfs. The intent is to reduce strength at game start vs the xenon,
/// not permanently cripple them.
///
/// Much of the functionality is about extracting and printing useful data about the jobs, so that we
/// can understand the massive amount of data relating to factions and their jobs across all the DLCs.

module X4.Jobs

open System.Xml.Linq
open FSharp.Data
open X4.Data
open X4.Utilities


let X4JobFileCore = X4UnpackedDataFolder + "/core/libraries/jobs.xml"
let X4JobFileTerran = X4UnpackedDataFolder + "/terran/libraries/jobs.xml"
let X4JobFilePirate = X4UnpackedDataFolder + "/pirate/libraries/jobs.xml"
[<Literal>] // split will be the template for normal job files, as the core game file doesn't use some tags (eg, 'preferbuilding')
let X4JobFileSplit = X4UnpackedDataFolder + "/split/libraries/jobs.xml"
[<Literal>] // We're going to use the boron as a template for diff/Add Job format file. Not all DLC use this format (eg, split, above).
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


// Since we're dealing with different job file formats between the base game and different mods, we need
// to convert them all to one canonical format for processing. We cheat a little, knowing that the type
// provider just puts a loose wrapping on top of the underlying XElement. IT's not strictly typesafe
// but it's safe within the scope of the data we're reading, and saves us writing something more complicated.
let getJobsFromDiff (diff:X4JobMod.Add[]) = 
    let jobsAdd = Array.filter (fun (add:X4JobMod.Add) -> add.Sel = "/jobs") diff
    [|  for jobs in jobsAdd do
            for job in jobs.Jobs do
                yield new X4Job.Job(job.XElement)
    |]



// Quotas may or may not exist. This is an easy function to multiply a quota if it's Some quota,
// or just return None if it's None. ie; a Option aware multiplier. Unlike normal integer math, This rounds UP.
let maybeMultiply (quota:Option<int>) (multiplier:float) =
    match quota with
    | Some q -> Some (int(ceil (float q * multiplier)))
    | None -> None


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


// Some jobs are given a specific sector or zone or cluster to spawn in. We want to move
// these to their new sectors if the old sector is no longer set as their primary territory.
// This function takes a job, and updates the 'location' property to the new sector.
let moveJobToSectorXml (id:string) (sector:string) (job:X4Job.Job) =
    // Load the existing location element
    let location = job.Location.XElement
    // Store the old macro value
    let oldMacro = location.Attribute("macro").Value

    // Update the class and macro attributes
    location.SetAttributeValue("class", "sector")
    location.SetAttributeValue("macro", sector)

    // Replace the old location element with the updated one
    let xml = new XElement("replace",
        new XAttribute("sel", $"//jobs/job[@id='{id}']/location"),
        location
    )
    printfn "  MOVING JOB %s from %s to sector %s " id oldMacro sector
    xml


// Build XML that will replace or add a 'prefer build' tag.
// We want to force many jobs for factions to be built at shipyards, rather than get spawned in automatically.
// Means the factions will start weaker, but, given time, will build up their fleets.
// I had considered the approach of splutting every job in two, one with preferbuild, the other without.
// this would spawn half of the ships, weakening the faction nicely, but it's more complicated and adds
// a lot of jobs. Instead, we're jjst going to target L and XL ships, and resupply ships.
let setPreferBuildXml (job:X4Job.Job)=
    let environment = new XElement("environment", [
            new XAttribute("preferbuilding", true),
            new XAttribute("buildatshipyard", true)
        ])
    let selector = new XAttribute("sel", $"//jobs/job[@id='{job.Id}']/environment")
    
    match job.Environment with
    | None ->
        // no existing line build an entire xml diff 'add' for the option.
        printfn "  ADDING JOB ENVIRONMENT AND BUILD SETTINGS %s preferbuild" job.Id
        new XElement("add", selector, environment)
    | Some environment ->
        // There's an existing environment, so we'll build and xml REPLACE based on the existing settings.
        printfn "  REPLACING JOB ENVIRONMENT BUILD SETTINGS %s " job.Id 
        new XElement("replace", selector, environment)


// Each job has a faction that it belongs to. The fields in the XML seem a bit unreliable
// USUALLY it seems to be dictated by the category.faction, but this doesn't always exist.
// If not, best guess seems to be look at the ship specified in the job, and use it's faction.
let getFaction (job:X4Job.Job) =
    // Category should give us the faction
    match job.Category with
    | Some category -> category.Faction
    | None ->
        match job.Ship with
        | Some ship -> ship.Select.Faction 
        | None ->
            // According to the debug dump, this never actually happens, but we'll Leave
            // the code here in case it does in a future DLC
            printfn "  WARNING!!!!! NO FACTION FOR JOB %s" job.Id
            "NONE"

// Does this job belong to a specific faction?
let isFaction(job, faction) = getFaction job = faction

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


// IT looks like the 'faction' in a job location does not mean 'put it in this factions territory',
// but instead also uses two other fields: 'relation' and 'comparison' to determine exactly where
// the job can be spawned.
// Basically, you compare the 'faction' name to 'relation' using 'comparison' operator.
// eg 'xenon' 'self' 'lt' seems to mean 'spawn in any territory less that is less than 'xenon':
// ie, all factions except xenon.
// faction="teladi" relation="ally" comparison="ge"  - any ally  (does 'ge' mean ally or my own territory?)
// faction="[teladi, ministry]" relation="self" comparison="exact" spawn only in teladi or ministry sectors.
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
// as it would be easy to overtune the xenon by accidentally exponentially
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

let isJobInFactionTerritory (job:X4Job.Job) =
    let faction = getFaction job  // Will never return "NONE" as we ignore minor tasks.
    let location = 
        match job.Location.Macro with
        | None -> "----"
        | Some location -> location
    
    match job.Location.Class with
    | "galaxy" -> true // well, if the class is galaxy, then definitely.
    | "cluster" -> doesFactionHavePresenceInLocationCluster faction location
    | "sector" -> if (isFactionInSector faction location) then true else false
    | "zone" -> 
        match findSectorFromZone location allSectors with
        | None -> failwith ("unable to find sector zone belongs to : " + location)
        | Some sector -> isFactionInSector faction sector
    | _ -> failwith ("Unhandled location class in job: " + job.Location.Class)


// Write some useful data about a job to the console. We use this purely to understand what jobs are
// doing, so we can make meaningful choices on how to change the economy and balance.
// This is not part of the mod generation, instead it helps us write the mod.
let printJobInfo (job:X4Job.Job) =
    let tags = "[" + (getJobTags job |> String.concat ", ") + "]"
    match isMinorTask job with
    | true ->
        printfn "IGNORING JOB %s, tags: %A" job.Id tags
    | false ->
        let faction = getFaction job
        // Extract data about the job, so we can summarise it.
        // Find out which jobs are outside of the factions sectors:
        let location = 
            match job.Location.Macro with
            | None -> "----"
            | Some location -> location

        let inTerritory = isJobInFactionTerritory job |> either "InTerritory" "No"
 
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
        let shipyard = match buildAtShipyard job with false -> "" | true -> "shipyard"
        let startactive = match isStartActive job with false -> "inactive" | true -> ""

        printfn "PROCESSING JOB %52s, %20s/%-32s: %12s, %8s / %-30s | %8s : %s %8s %6s %8s %8s %11s %-46s escorts: %s"
             job.Id (getFaction job) (getLocationFactionRelation job) inTerritory job.Location.Class location shipSize quota subordinates subordinate startactive preferBuild shipyard tags subordinatelist


// Print out the number of jobs in each set of category 'tags'. Again, not part of the mod generation,
// but used as we write the mod to understand the massive amounts of data.
let printJobCategoryCount allJobs =
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


// 'Jobs' impacts the ships that are spawned. We're going to do one of two things:
// 1. For XENON, increase the quotas for their ships, military and civilian.
// We'll increase the military more than the economy, so that the Xenon start with strong
// coverage across the galaxy, but not completely overwelming economy. We want to make the game
// a bit harder, but not too much. And in a long term game, it's the economy that matters.
// For military ships, we'll tune differently based on size. Increase X and L ships by a little,
// but S & M by a lot: Lets have plenty small patrols causing chaos, but only a few big station busters.
//
// 2. For other factions, we can't just reduce job quotas, as this impact the max number of ships
// that they will *ever* build. Instead, we'll set the 'preferbuild' tag so that they have to
// build their ships at shipyards, rather than getting them spawned in automatically at game start
// So, this mean the factions will start weaker, but, given time, will build up their fleets if
// their economy is strong enough.
// We will only change military jobs, and just for X and XL ships and resupply ships. Factions will be
// weak enough already with reduced territories, mining, and stations impacting economy. We don't
// want to overdo it by reducing starting economy ships too.
//
// Originally, I was going to move certain L/XL fleets from their old sectors in to their
// new sectors, but I've decided to leave them where they are. I want to encourage the AI
// to be a bit agressive and try reclaim their territory once they build these fleets.
// The station gate defense should be enough to keep the xenon at bay without these.
let processJob (job:X4Job.Job) =
    printJobInfo job
    let size = match job.Category with | None -> "NONE" | Some category -> Option.defaultValue "NONE" category.Size

    if not (isStartActive job) then None        // Any job that is not active at game start can be ignored. These are usually plot/story progress related.
    else if isSubordinate job then None         // these are subordinate/escort ships. Leave them alone, as they won't spawn unless their parent does.
    else if isMinorTask job then None           // These are minor tasks, like police or generic mass traffic. Leave them alone.
    else if isFaction(job, "xenon") then
        // XENON: Determine the quota mupliplier based on ship size and military/civilian
        let multiplier =
            match isMilitaryJob job, size with
            | true, "ship_xl" -> 1.5    // battleships and carriers
            | true, "ship_l"  -> 1.5    // destroyers: of which Xenon should have none in vanilla
            | true, _         -> 1.7    // S and M military ships
            | false, _        -> 1.3    // s & m civilian ships

        // Calculate the new quotas.
        let galaxyQuota = maybeMultiply job.Quota.Galaxy multiplier
        let maxGalaxyQuota = maybeMultiply job.Quota.Maxgalaxy multiplier
        let clusterQuota = maybeMultiply job.Quota.Cluster multiplier
        let sectorQuota = maybeMultiply job.Quota.Sector multiplier

        // We only need to create a replace tag if we're actually changing something. Check if any quotas are 'Some x'.
        if List.exists Option.isSome [galaxyQuota; maxGalaxyQuota; clusterQuota; sectorQuota] then
            Some  (replaceQuotaXml job.Id (galaxyQuota |> Option.defaultValue 0) maxGalaxyQuota clusterQuota sectorQuota)
        else None

    // Handle the NON-XENON factions
    else if not (isMilitaryJob job) then None   // Leave the economy alone.
    else if List.contains size ["ship_s"; "ship_m"] then None  // We don't care about small ships, just the big ones:
    else
        // So now, all we'll do is set the 'preferbuild' tag to make sure the factions start the game
        // without their big cvarrier and destroyer fleets, and must build them slowly as the game progresses.
        if not (isPreferBuild job) then Some (setPreferBuildXml job)
        else None


// Kick off the work of generating the job file for the mod, and write out the
// XML diff to the given filename
let generate_job_file (filename:string) =
    // Load all the job data from the core game and expansions, and merge in to one list.
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
    printJobCategoryCount allJobs

    // Now process all the jobs, getting a list containing only the changes that we're making.
    let jobDiff = [|
        for job in allJobs do
            match processJob job with Some xmldiff -> yield xmldiff | _ -> ()
    |]

    // Prepare to write out the XML for the mod. Start by creating an XML DIFF object from the template
    let outJobFile = X4JobMod.Parse(X4JobModTemplate)
    let diff = outJobFile.XElement // the root element is actually the 'diff' tag.

    // Now add out job changes, one by one, to the mutable diff element
    [| for element in jobDiff do
        diff.Add(element)
        diff.Add( new XText("\n")) // Add a newline after each element so the output is readible
    |] |> ignore

    X4.Utilities.write_xml_file filename outJobFile.XElement
  