/// <summary>
/// The jobs.xml side of the data layer: loads the vanilla job definitions from the
/// core game and every DLC (two file formats: plain jobs files and diff files) and
/// exposes them as the pure Job records.
/// (Phase G of the refactor will also move the WRITING of the mod's jobs.xml here.)
/// </summary>
[<AutoOpen>]
module X4.Data.JobData

open System.Xml.Linq
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


// ==== WRITER ====
// The mod's jobs.xml is produced here from the pure JobDirective values decided by
// the jobs logic. The XML construction is inherited verbatim from the original code.

// Construct an XML element representing a 'replace' tag that will replace the quotas for a given job.
// Important note: stations and products have a QUOTAS section containing a list of quotas, but JOBS
// have only a single QUOTA element, and no quotas list.
// example replace line:
// <replace sel="/jobs/job[@id='xen_energycells']/@quota">
//      <quota galaxy="42" cluster="3"/>
// </replace>
let private replaceQuotaXml (id: string) (galaxy: int) (maxGalaxy: Option<int>) (cluster: Option<int>) (sector: Option<int>) =
    let quota = [
        yield new XAttribute("galaxy", galaxy)
        match maxGalaxy with
        | Some x -> yield new XAttribute("maxgalaxy", x)
        | _ -> ()
        match cluster with
        | Some x -> yield new XAttribute("cluster", x)
        | _ -> ()
        match sector with
        | Some x -> yield new XAttribute("sector", x)
        | _ -> ()
    ]

    let xml =
        new XElement("replace", new XAttribute("sel", $"//jobs/job[@id='{id}']/quota"), new XElement("quota", quota))

    printfn "     REPLACING JOB QUOTA %s with gal:%A maxGal:%A clust:%A sect:%A" id galaxy maxGalaxy cluster sector
    xml


// Build XML that will replace or add a 'prefer build' tag.
// We want to force many jobs for factions to be built at shipyards, rather than get spawned in automatically.
// Means the factions will start weaker, but, given time, will build up their fleets.
// I had considered the approach of splutting every job in two, one with preferbuild, the other without.
// this would spawn half of the ships, weakening the faction nicely, but it's more complicated and adds
// a lot of jobs. Instead, we're jjst going to target L and XL ships, and resupply ships.
let private setPreferBuildXml (job: Job) =
    let selector = new XAttribute("sel", $"//jobs/job[@id='{job.Id}']/environment")

    match job.Environment with
    | None ->
        // no existing line build an entire xml diff 'add' for the option.
        printfn "  ADDING JOB ENVIRONMENT AND BUILD SETTINGS %s preferbuild" job.Id

        let environment =
            new XElement(
                "environment",
                [
                    new XAttribute("preferbuilding", true), new XAttribute("buildatshipyard", true)
                ]
            )

        new XElement("add", selector, environment)

    | Some environment ->
        // There's an existing environment, so we'll build and xml REPLACE based on the existing settings.
        printfn "  REPLACING JOB ENVIRONMENT BUILD SETTINGS %s " job.Id
        let newEnvironment = new XElement(XmlSource.value environment.Source)
        newEnvironment.SetAttributeValue("preferbuilding", true)
        newEnvironment.SetAttributeValue("buildatshipyard", true)
        let replacement = new XElement("replace", selector, newEnvironment)
        replacement


// This string is the starting point for the output job we'll write.
// The jobs file also already contains some predefined xenon jobs we're adding to TER territory.
let X4JobModTemplate =
    System.IO.File.ReadAllText(__SOURCE_DIRECTORY__ + "/mod_templates/jobs.xml")

let private jobDirectiveXml (directive: JobDirective) =
    match directive with
    | ReplaceJobQuota(id, galaxy, maxGalaxy, cluster, sector) -> replaceQuotaXml id galaxy maxGalaxy cluster sector
    | SetPreferBuild job -> setPreferBuildXml job

// Write the mod's jobs.xml diff from the job directives, seeded from the hand written
// template (which already contains some predefined xenon jobs for TER territory).
let writeJobsFile (filename: string) (directives: JobDirective list) =
    // Prepare to write out the XML for the mod. Start by creating an XML DIFF object from the template
    let outJobFile = X4JobMod.Parse(X4JobModTemplate)
    let diff = outJobFile.XElement // the root element is actually the 'diff' tag.

    // Now add out job changes, one by one, to the mutable diff element
    for directive in directives do
        diff.Add(jobDirectiveXml directive)
        diff.Add(new XText("\n")) // Add a newline after each element so the output is readible

    X4.WriteModfiles.write_xml_file "core" filename outJobFile.XElement
