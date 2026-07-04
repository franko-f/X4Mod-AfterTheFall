/// <summary>
/// The jobs.xml side of the data layer: loads the vanilla job definitions from the
/// core game and every DLC (two file formats: plain jobs files and diff files) and
/// exposes them as the pure Job records.
/// (Phase G of the refactor will also move the WRITING of the mod's jobs.xml here.)
/// </summary>
[<AutoOpen>]
module X4.Data.JobData

open FSharp.Data
open X4.Types
open X4.Data.Xml

let X4JobFileCore = X4UnpackedDataFolder + "/libraries/jobs.xml"

let X4JobFileTerran =
    X4UnpackedDataFolder + "/extensions/ego_dlc_terran/libraries/jobs.xml"

let X4JobFilePirate =
    X4UnpackedDataFolder + "/extensions/ego_dlc_pirate/libraries/jobs.xml"

[<Literal>] // split will be the template for normal job files, as the core game file doesn't use some tags (eg, 'preferbuilding')
let X4JobFileSplit =
    X4UnpackedDataFolder + "/extensions/ego_dlc_split/libraries/jobs.xml"

[<Literal>] // We're going to use the boron as a template for diff/Add Job format file. Not all DLC use this format (eg, split, above).
let X4JobFileBoron =
    X4UnpackedDataFolder + "/extensions/ego_dlc_boron/libraries/jobs.xml"

// two job file formats to parse:
// One normal game XML file, and the other is a DIFF file with jobs inside an 'add' selector tag.
type X4Job = XmlProvider<X4JobFileSplit>
type X4JobMod = XmlProvider<X4JobFileBoron> // Use this as a sample file so we can parse the DLC jobs files that use the DIFF format.

// Since we're dealing with different job file formats between the base game and different mods, we need
// to convert them all to one canonical format for processing. We cheat a little, knowing that the type
// provider just puts a loose wrapping on top of the underlying XElement. IT's not strictly typesafe
// but it's safe within the scope of the data we're reading, and saves us writing something more complicated.
let private getJobsFromDiff (diff: X4JobMod.Add[]) =
    let jobsAdd = Array.filter (fun (add: X4JobMod.Add) -> add.Sel = "/jobs") diff

    [|
        for jobs in jobsAdd do
            for job in jobs.Jobs do
                yield new X4Job.Job(job.XElement)
    |]

// Conversion from the provider type to the pure Job record: every field the job
// processing logic reads, flattened. Environment keeps the provider-parsed element
// as its Source so the preferbuild rewrite can clone it byte-identically.
let private toJob (job: X4Job.Job) : Job = {
    Id = job.Id
    Category =
        job.Category
        |> Option.map (fun category -> {
            Faction = category.Faction
            Tags = category.Tags
            Size = category.Size
        })
    Quota = {
        Galaxy = job.Quota.Galaxy
        Maxgalaxy = job.Quota.Maxgalaxy
        Cluster = job.Quota.Cluster
        Sector = job.Quota.Sector
        Wing = job.Quota.Wing
    }
    Location = {
        Class = job.Location.Class
        Macro = job.Location.Macro
        Faction = job.Location.Faction
        Relation = job.Location.Relation
        Comparison = job.Location.Comparison
    }
    Environment =
        job.Environment
        |> Option.map (fun environment -> {
            Preferbuilding = environment.Preferbuilding
            Buildatshipyard = environment.Buildatshipyard
            Source = XmlSource.ofElement environment.XElement
        })
    ShipSelectFaction = job.Ship |> Option.map (fun ship -> ship.Select.Faction)
    Task = job.Task |> Option.map (fun task -> task.Task)
    HasSubordinateModifier =
        job.Modifiers
        |> Option.exists (fun modifiers -> Option.isSome modifiers.Subordinate)
    SubordinateJobs =
        job.Subordinates
        |> Option.map (fun subordinates -> subordinates.Subordinates |> Array.map (fun subordinate -> subordinate.Job))
    Startactive = job.Startactive
}

// Load all the job data from the core game and expansions, and merge in to one list.
// ORDER MATTERS for byte-identical output: core, split, pirate, terran, boron -
// the generated jobs.xml diff operations appear in this processing order.
let allJobs: Job list =
    let X4JobsCore = X4Job.Load(X4JobFileCore)
    let X4JobsSplit = X4Job.Load(X4JobFileSplit) // Split don't use a diff file.
    let X4JobsPirate = X4Job.Load(X4JobFilePirate) // same for pirate.
    let X4JobsBoron = X4JobMod.Load(X4JobFileBoron)
    let X4JobsTerran = X4JobMod.Load(X4JobFileTerran)

    Array.concat [
        X4JobsCore.Jobs
        X4JobsSplit.Jobs
        X4JobsPirate.Jobs
        getJobsFromDiff X4JobsTerran.Adds
        getJobsFromDiff X4JobsBoron.Adds
    ]
    |> Array.toList
    |> List.map toJob
