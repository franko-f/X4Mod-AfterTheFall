/// Parse the X4 jobs file from the core game and the DLCs.
/// Process job quotas, reducing AI faction ships and fleets, but increase the  Xenons ships and fleets.
/// We try to only impact the *initial* job quotas, so that the AI factions can still build up their
/// fleets using their shipyards and wharfs. The intent is to reduce strength at game start vs the xenon,
/// not permanently cripple them.
///
/// Much of the functionality is about extracting and printing useful data about the jobs, so that we
/// can understand the massive amount of data relating to factions and their jobs across all the DLCs.

module X4.Jobs

open X4.Data
open X4.Types
open X4.Utilities


// Quotas may or may not exist. This is an easy function to multiply a quota if it's Some quota,
// or just return None if it's None. ie; a Option aware multiplier. Unlike normal integer math, This rounds UP.
let maybeMultiply (quota: Option<int>) (multiplier: float) =
    match quota with
    | Some q -> Some(int (ceil (float q * multiplier)))
    | None -> None


// Each job has a faction that it belongs to. The fields in the XML seem a bit unreliable
// USUALLY it seems to be dictated by the category.faction, but this doesn't always exist.
// If not, best guess seems to be look at the ship specified in the job, and use it's faction.
let getFaction (job: Job) =
    // Category should give us the faction
    match job.Category with
    | Some category -> category.Faction
    | None ->
        match job.ShipSelectFaction with
        | Some faction -> faction
        | None ->
            // According to the debug dump, this never actually happens, but we'll Leave
            // the code here in case it does in a future DLC
            printfn "  WARNING!!!!! NO FACTION FOR JOB %s" job.Id
            "NONE"

// Does this job belong to a specific faction?
let isFaction (job, faction) = getFaction job = faction

// kind of the opposite of getJobFaction - We discover in which factions sectors
// this job is allowed. I assume it's associated with Galaxy class
let getLocationFaction (job: Job) =
    match job.Location.Faction with
    | None ->
        // if location faction is not specified, then we assume it's the same as 'category'
        match job.Category with
        | None ->
            printfn "  NO FACTION FOR JOB %s" job.Id
            None
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
let getLocationFactionRelation (job: Job) =
    let faction = getLocationFaction job |> Option.defaultValue "NONE"

    match job.Location.Comparison, job.Location.Relation with
    | None, None -> sprintf "%s" faction
    | Some comparison, Some relation -> sprintf "%s (%s) %s" faction comparison relation
    | _ -> sprintf "%s" faction // This case never happens in the current data as of DLC 4:Boron

// Checks if it's a police job, or lacks a faction, or something else
// that makes it sa non standard faction job that were not interested in.
// Returns None if we should ignore it, or Some FactionName
let isMinorTask (job: Job) =
    job.Task
    |> Option.exists (fun task -> task = "masstraffic.generic" || task = "masstraffic.police")

// Subordinate jobs are things like escorts or 'subordinate' ships.
// They're wings of fighters on carriers, etc. We want to ignore these,
// as it would be easy to overtune the xenon by accidentally exponentially
// increasing the number of ships in a fleet.
let isSubordinate (job: Job) =
    job.HasSubordinateModifier

// Does this ship have subordinates? ie, is it a carrier? Destroyer group?
let hasSubordinate (job: Job) = Option.isSome job.SubordinateJobs

let subordinateIds (job: Job) =
    match job.SubordinateJobs with
    | None -> [| "" |]
    | Some subordinates -> subordinates

// Some jobs are flagged to start immediately when the game begins.
// Other jobs only activate on a given trigger. We want to ignore those.
// defaults to true when not set
let isStartActive (job: Job) =
    job.Startactive |> Option.defaultValue true

let getTagList (job: Job) =
    match job.Category with
    | None -> []
    | Some category -> Utilities.parseStringList category.Tags

let isMilitaryJob (job: Job) =
    getTagList job
    |> List.exists (fun tag -> List.contains tag [ "military"; "plunderer" ])

// 'preferbuilding' means that the ships won't be autospawned. In theory, they
// will get queued to build at shipyards instead.
let isPreferBuild (job: Job) =
    match job.Environment with
    | None -> false
    | Some environment -> environment.Preferbuilding |> Option.defaultValue false

let buildAtShipyard (job: Job) =
    match job.Environment with
    | None -> false
    | Some environment -> environment.Buildatshipyard

let isJobInFactionTerritory (job: Job) =
    let faction = getFaction job // Will never return "NONE" as we ignore minor tasks.

    let location =
        match job.Location.Macro with
        | None -> "----"
        | Some location -> location

    isFactionInLocation faction location job.Location.Class


// Write some useful data about a job to the console. We use this purely to understand what jobs are
// doing, so we can make meaningful choices on how to change the economy and balance.
// This is not part of the mod generation, instead it helps us write the mod.
let printJobInfo (job: Job) =
    let tags = "[" + (getTagList job |> String.concat ", ") + "]"

    match isMinorTask job with
    | true -> printfn "IGNORING JOB %s, tags: %A" job.Id tags
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
            let galaxy = job.Quota.Galaxy |> Option.defaultValue 0
            let maxGalaxy = job.Quota.Maxgalaxy |> Option.defaultValue 0
            let sector = job.Quota.Sector |> Option.defaultValue 0
            let cluster = job.Quota.Cluster |> Option.defaultValue 0
            let wing = job.Quota.Wing |> Option.defaultValue 0
            sprintf "%3d/%-3d, %3d, %3d, %3d" galaxy maxGalaxy cluster sector wing

        let subordinate = isSubordinate job |> either "escort" ""
        let subordinates = hasSubordinate job |> either "escorted" ""
        let subordinatelist = subordinateIds job |> String.concat ", "

        let preferBuild =
            match isPreferBuild job with
            | false -> ""
            | true -> "preferbuild"

        let shipyard =
            match buildAtShipyard job with
            | false -> ""
            | true -> "shipyard"

        let startactive =
            match isStartActive job with
            | false -> "inactive"
            | true -> ""

        printfn
            "PROCESSING JOB %52s, %20s/%-32s: %12s, %8s / %-30s | %8s : %s %8s %6s %8s %8s %11s %-46s escorts: %s"
            job.Id
            (getFaction job)
            (getLocationFactionRelation job)
            inTerritory
            job.Location.Class
            location
            shipSize
            quota
            subordinates
            subordinate
            startactive
            preferBuild
            shipyard
            tags
            subordinatelist


// Print out the number of jobs in each set of category 'tags'. Again, not part of the mod generation,
// but used as we write the mod to understand the massive amounts of data.
let printJobCategoryCount allJobs =
    let categoryTags =
        allJobs
        |> List.fold
            (fun (tags: Map<string list, int>) (job: Job) ->
                let jobtags = getTagList job

                match Map.tryFind jobtags tags with
                | None -> Map.add jobtags 1 tags
                | Some count -> Map.add jobtags (count + 1) tags)
            Map.empty
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
let processJob (job: Job) =
    printJobInfo job

    // Given a quota and a multiplier, apply the multiplier to any quota element that is actually set for the job.
    // A quota that is None is still None. Some quota will become Some quota*multiplier, for each cluster/sector/etc quota
    let quotaMultiply (quota: JobQuota) multiplier =
        let galaxyQuota = maybeMultiply quota.Galaxy multiplier
        let maxGalaxyQuota = maybeMultiply quota.Maxgalaxy multiplier
        let clusterQuota = maybeMultiply quota.Cluster multiplier
        let sectorQuota = maybeMultiply quota.Sector multiplier
        galaxyQuota, maxGalaxyQuota, clusterQuota, sectorQuota

    // If the job has any non-None quotas, multiply it, then create the new quota replacement XML
    let maybeGenerateQuotaReplacementXML (quota: JobQuota) multiplier =
        // Calculate the new quotas.
        let galaxyQuota, maxGalaxyQuota, clusterQuota, sectorQuota =
            quotaMultiply quota multiplier
        // We only need to create a replace tag if we're actually changing something. Check if any quotas are 'Some x'.
        if List.exists Option.isSome [ galaxyQuota; maxGalaxyQuota; clusterQuota; sectorQuota ] then
            Some(ReplaceJobQuota(job.Id, (galaxyQuota |> Option.defaultValue 0), maxGalaxyQuota, clusterQuota, sectorQuota))
        else
            None

    let size =
        match job.Category with
        | None -> "NONE"
        | Some category -> Option.defaultValue "NONE" category.Size

    if not (isStartActive job) then
        None // Any job that is not active at game start can be ignored. These are usually plot/story progress related.
    else if isSubordinate job then
        None // these are subordinate/escort ships. Leave them alone, as they won't spawn unless their parent does.
    else if isMinorTask job then
        None // These are minor tasks, like police or generic mass traffic. Leave them alone.
    else if isFaction (job, "xenon") then
        // XENON: Determine the quota mupliplier based on ship size and military/civilian
        let multiplier =
            match isMilitaryJob job, size with
            | true, "ship_xl" -> X4.Tuning.Jobs.XenonMilitaryXLMultiplier // battleships and carriers
            | true, "ship_l" -> X4.Tuning.Jobs.XenonMilitaryLMultiplier // destroyers
            | true, _ -> X4.Tuning.Jobs.XenonMilitarySMMultiplier // S and M military ships
            | false, _ -> X4.Tuning.Jobs.XenonCivilianMultiplier // s & m civilian ships

        maybeGenerateQuotaReplacementXML job.Quota multiplier

    // Handle the NON-XENON factions
    else if not (isMilitaryJob job) then
        None // Leave the economy alone.
    // reduce the number of pirates, as they're much more dangerous to the weakened economies.
    else if
        isFaction (job, "scaleplate")
        || isFaction (job, "fallensplit")
        || isFaction (job, "yaki")
    then
        maybeGenerateQuotaReplacementXML job.Quota X4.Tuning.Jobs.PirateMilitaryMultiplier

    else if List.contains size [ "ship_s"; "ship_m" ] then
        None // We don't care about small ships, just the big ones:
    else if
        // So now, all we'll do is set the 'preferbuild' tag to make sure the factions start the game
        // without their big cvarrier and destroyer fleets, and must build them slowly as the game progresses.
        not (isPreferBuild job)
    then
        Some(SetPreferBuild job)
    else
        None


// Kick off the work of generating the job file for the mod, and write out the
// XML diff to the given filename
let generate_job_file (filename: string) =
    // lets find out some interesting things about jobs:
    printJobCategoryCount allJobs

    // Now process all the jobs, getting a list containing only the changes that we're
    // making, and hand the directives to the data layer to write the mod's jobs.xml.
    [
        for job in allJobs do
            match processJob job with
            | Some directive -> yield directive
            | _ -> ()
    ]
    |> writeJobsFile filename
