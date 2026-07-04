// The 'god.xml' file defines the starting state of the universe, in particular the starting
// unique stations and random factories for each faction that are scattered around the
// map. I'll call it 'world start' to make it a little clearer.
// There are two primary sets of data in this file we're interested in:
// 1. STATIONS : These are manually placed unique stations, shipyards, tradeposts and defense stations.
//  My understanding that it's the placement of these stations that will determine faction ownership
// of the sectors. Unlike 'products', they're given an explicit sector they spawn in, and sometimes
// a specific location in the sector. This is different from Product factories which are goverened by:
// 2. PRODUCTS: This defines the production factories in the universe. Unlike STATIONS, they are
// given a descriptor that describes the product they will produce, the faction they belong to,
// and the maximum that can exist for the faction in the galaxy. Then the game randomly creates
// and scatters these stations around the galaxy.
//
// For the purpose of this mod, we're going to REMOVE almost all the faction defence stations,
// and REPLACE them with Xenon ones instead. Kh'aak will remain unaffected.
//
// We'll manually assign a sector or two to each faction by adding a defence stations, shipyard, wharf
// and add a few resources to those sectors so the factions can survive.
// PRODUCT factories will be updated to have a lot less stations to weaken the faction and also ensure
// their remaing sectors aren't overloaded with factories.
// Lastly, we'll manually position a very powerful defense station in front of the gate to protect the
// sector and help avoid the Xenon just running all over the weaker faction. They should be stopped by
// the station, giving a safe zone for the player, and a place to collect loot/get some action.
//
// Later, I may add a few more abandoned destroyers/carriers and smaller ships around the galaxy
// for the player to find.


module X4.God

open System.Xml
open System.Xml.Linq
open X4.Utilities
open X4.Data
open X4.Territories

open X4.Tuning // Economy ratios and gate defence values live in tuning.fs


// the 'log' functions just extract a bit of data about a station, and log it
// to the terminal for debugging and tracking purposes.
let logStation (action: string) (station: X4WorldStart.Station) =
    let tags =
        match station.Station.Select with
        | Some tag -> tag.Tags
        | _ -> "[none]"

    printfn
        "%s STATION %s race: %A, owner: %A, type: %A, location: %A:%A, id: %A, station: %A   "
        action
        tags
        station.Race
        station.Owner
        station.Type
        station.Location.Class
        station.Location.Macro
        station.Id
        station.Station.Macro

let logAddStation (action: string) (station: X4GodMod.Station) =
    let tags =
        match station.Station.Select with
        | Some tag -> tag.Tags
        | _ -> "[none]"

    printfn
        "   %s STATION %s race: %s, owner: %s, type: %s, location: %s:%s, id: %s, station: \"none\"   "
        action
        tags
        station.Race
        station.Owner
        station.Type
        station.Location.Class
        station.Location.Macro
        station.Id

let logProduct (product: X4WorldStart.Product) =
    printfn
        "PROCESSING PRODUCT [%s:%s] %s/%s with quotas %i/%i"
        product.Owner
        (product.Location.Faction |> Option.defaultValue "UNKNOWN")
        product.Type
        product.Ware
        product.Quota.Galaxy
        (product.Quota.Sector |> Option.defaultValue -1)


// Given a selector ID, search an instance of a GodMod xml file for the 'ADD' section
// with that 'sel' value. This will be the XElement we will manipulate.
let find_add_selector sel xml =
    Array.find (fun (elem: X4GodMod.Add) -> elem.Sel = sel) xml


let stationSectorName (station: X4WorldStart.Station) =
    match station.Location.Class with
    | Some "zone" ->
        findSectorFromZone (station.Location.Macro |> Option.defaultValue "")
        |> Option.defaultValue "none"
    | Some "sector" -> station.Location.Macro |> Option.defaultValue "none"
    | _ -> "none"

// Is this station in a sector we're going to leave alone? ie, in the territory of the owning faction
// or a faction we're ignoring and not changing?
let ignoreStation (station: X4WorldStart.Station) =
    let sector = stationSectorName station
    let inTerritory = isFactionInSector station.Owner sector // Station already in the territory of the owning faction

    let inFriendlyTerritory =
        List.exists (fun faction -> isFactionInSector faction sector) [
            "teladi"
            "paranid"
            "holyorder"
            "split"
            "freesplit"
            "argon"
            "antigone"
            "hatikvah"
            "ministry"
        ] // Station is in the territory of a fr

    let isIgnored =
        List.contains station.Owner [
            "khaak"
            "xenon"
            "yaki"
            "scaleplate"
            "buccaneers"
            "player"
            "kaori"
            "holyorderfanatic"
        ]

    let isPirateBase =
        station.Station.Select |> Option.exists (fun s -> s.Tags = "[piratebase]")

    inTerritory || isIgnored || isPirateBase || inFriendlyTerritory


// Find and return the first occurrence of a station with the given faction and type.
// Used to find things like faction defence stations, wharfs, etc, so that we can move
// the first instance to the factions 'safe' location, while removing the rest.
// Sometimes the station type is determined by the value of 'station.type', but other times,
// station.type is set to 'factory', and you must look to the 'tags' field to determine the
// type of station.
let findStation (faction: string) (stationType: string) (stations: X4WorldStart.Station list) =
    match
        List.tryFind
            (fun (station: X4WorldStart.Station) -> station.Owner = faction && station.Type = Some stationType)
            stations
    with
    | Some station -> Some station
    | None ->
        // Ok, this might be a case where station type is 'factory', and we need to look at station.station.select.tags
        match
            List.tryFind
                (fun (station: X4WorldStart.Station) ->
                    // if the station is owned by the correct faction, then attempt to extract the tags by working through
                    // the list of option types stored in station.station.select.tags to get to the actual tags (if they exist)
                    // then finally check if the tags (a comma separated string) contain the stationType we're looking for.
                    station.Owner = faction
                    && station.Station.Select
                       |> Option.map (fun x -> x.Tags)
                       |> (Option.defaultValue "")
                       |> fun tags -> tags.Contains stationType)
                stations
        with
        | Some station -> Some station
        | None ->
            // And some terran/PIO defence stations don't have trags either. They use station.constructionplan
            List.tryFind
                (fun (station: X4WorldStart.Station) ->
                    station.Owner = faction
                    && station.Station.Constructionplan
                       |> Option.map (fun x -> x)
                       |> (Option.defaultValue "")
                       |> fun tags -> tags.Contains stationType)
                stations


// MAIN PROCESSING FUNCTIONS

// Given a station, process it according to our rules. We may replace it
// with a Xenon one, remove it, etc. This function is call once per station
// stationsToMove are the IDs of stations that we're going to move to a safe sector, rather than
// completely replace by a Xenon one. We'll still put a xenon station where they used to be
let processStation
    (station: X4WorldStart.Station)
    (stationsToMove: string list)
    (xenonShipyard: XElement)
    (xenonWharf: XElement)
    (xenonDefence: XElement)
    =
    logStation "PROCESSING" station

    // So, turns out XmlProvider is more focused around reads. Writes are not... great.
    // To edit underlying fields, you really need to get to the underlying linq XElement
    // and then manipulate that. When trying to create a new XML Provider element based
    // off a copy of an old one, it will use a reference to the underlying XElement.
    // that means our edits will overwrite, sooo we need to clone the XElement each time.

    match ignoreStation station with
    | true ->
        // This station is in a sector we're leaving alone..
        printfn "  LEAVING [%s]:%s :: %A" station.Owner (stationSectorName station) station.Id
        (None, None, None)
    | _ ->
        // 'Select' contains the tags that describe whether this is a defence station, wharf or shipyard.
        let stationClone =
            match station.Station.Select, station.Type with
            | (None, Some "tradingstation") ->
                // These seem to be teladi tranding stations. Replace with something more... interesting
                Some(new XElement(xenonWharf))
            | (None, Some "factory") ->
                // MOST examples in the logs without a tag all seem to be scenarios we weren't going to replace. Khaak, xenon, etc.
                // But TERRAN/SEG has a few defence stations without tags. We'll check their construction plan instead.
                match station.Station.Constructionplan with
                | Some "'ter_defence'" -> Some(new XElement(xenonDefence))
                | Some "'ter_defenceplatform'" -> Some(new XElement(xenonDefence))
                | Some "'pio_defence'" -> Some(new XElement(xenonDefence))
                | _ -> None

            | (None, _) ->
                // the other examples in the logs without a tag all seem to be scenarios we weren't going to replace. Khaak, xenon, etc.
                None
            | (Some select, _) ->
                // create the new xelement clone so we can edit it later as part of the replacement station.
                // We're going to replace different types of NPC buildings with different Xenon stations.
                match select.Tags with
                | "[shipyard]" -> Some(new XElement(xenonShipyard))
                | "[wharf]"
                | "[equipmentdock]" -> Some(new XElement(xenonWharf))
                | "[defence]"
                | "[tradestation]"
                | "[piratebase]" -> Some(new XElement(xenonDefence)) // For now, replace HAT piratebase with xenon defense.
                | x ->
                    printfn "UNHANDLED STATION TYPE: %s - DEFAULTING TO XENON DEFENCE" x
                    Some(new XElement(xenonDefence))

        match stationClone with
        | None ->
            printfn "  IGNORING DEFAULT [%s]" station.Owner // These will still exist, and probably get wiped pretty quick, unless they're well hidden.
            (None, None, None)

        | Some stationClone ->
            // ok, so we're going to replace this station with a Xenon one. This means we need to do a couple of things:
            // 1. Give the clone a new ID based off the old stations ID
            // 2. Create some XML that will remove the old station from the game. Later, we're toing to check the list of
            //   remove stations and actually move a few of them to a new location instead.
            let id = station.Id
            let locationClass = Option.defaultValue "none" station.Location.Class
            let locationMacro = Option.defaultValue "none" station.Location.Macro

            let cluster =
                findClusterFromLocation locationClass locationMacro
                |> Option.defaultValue "none" // Find out which cluster this location is in.

            let replacement =
                match X4.Territories.neutralClusters |> List.contains cluster with
                | true ->
                    // If this is a neutral cluster, then we're going to clear all stations out of it.
                    // ie; we're not going to replace this station with a xenon whatever.
                    None
                | false ->
                    // create a new Xenon station to replace it
                    let replacement = new X4GodMod.Station(stationClone)
                    replacement.XElement.SetAttributeValue(XName.Get("id"), replacement.Id + "_x_" + id) // Give it a new unique ID
                    // update location. As they're different types (as far as the type provider is concerned), we have to manually set
                    // the important zone and macro fields.
                    replacement.Location.XElement.SetAttributeValue(XName.Get("class"), locationClass)
                    replacement.Location.XElement.SetAttributeValue(XName.Get("macro"), locationMacro)
                    logAddStation "REPLACE" replacement
                    Some replacement.XElement

            // Now that's done, decide whether to REMOVE or MOVE the old station.
            if List.contains id stationsToMove then
                // We're going to move this station to a safe sector, rather than completely replace it with Xenon.
                // We'll still put a xenon station where they used to be
                // Select a random safe sector for the station to move to.
                // Use the shared seeded generator so the output is reproducible run to run.
                let sectors = getFactionSectors station.Owner
                let randomSector = sectors.[rand.Next(sectors.Length)]

                printfn
                    "  MOVING [%s]:%s :: %A  from  %A:%A to sector:%A"
                    station.Owner
                    (stationSectorName station)
                    station.Id
                    locationClass
                    locationMacro
                    randomSector
                // build the XML that will update the old stations location.
                let replaceXml = [
                    new XElement(
                        "replace",
                        new XAttribute("sel", $"//god/stations/station[@id='{id}']/location/@class"),
                        "sector"
                    )
                    new XElement(
                        "replace",
                        new XAttribute("sel", $"//god/stations/station[@id='{id}']/location/@macro"),
                        randomSector
                    )
                ]

                (replacement, None, Some replaceXml)
            else
                // create XML tag that will remove the old station
                let remove =
                    new XElement(
                        "remove",
                        new XAttribute("sel", $"//god/stations/station[@id='{id}']") // XML remove tag for the station we're replacing with Xenon.
                    )

                (replacement, Some remove, None) // return an add/remove options,


// with the shifting around of valid territory, the various races have lost some of their critical wharfs and shipyards.
// We need to re-add some, but not all. Just make sure each faction has at least one of each type.
let findStationsThatNeedMoving (stations: X4WorldStart.Station list) =
    stations
    // We're looking for the station we did NOT ignore earlier: ie, the ones that may have been replaced.
    |> List.filter (fun (station: X4WorldStart.Station) -> ignoreStation station = false)
    // But they may have been ignored because they belong to a faction we're not touching
    |> List.filter (fun station ->
        not (List.contains station.Owner [ "khaak"; "xenon"; "yaki"; "scaleplate"; "buccaneers"; "player" ]))
    // And we're only interested in the ones that are shipyards, wharfs, trading stations, etc.
    |> List.filter (fun station ->
        // most special stations are identified by tags in the optional 'select' field.
        station.Station.Select
        |> Option.exists (fun s ->
            List.contains (s.Tags) [ "[shipyard]"; "[wharf]"; "[equipmentdock]"; "[tradestation]" ])
        || station.Type = Some "tradingstation" // Teladi trading stations are identified differently, by using type.
    )
    // BUT, we only want to move the first instance of each type of station per fection, so lets drop duplicates.
    |> List.distinctBy (fun station -> (station.Owner, station.Type, station.Station.Select))



// Construct an XML element representing a 'replace' tag that will replace a specific quota for a given product.
// example replace line:
// <replace sel="/god/products/product[@id='arg_graphene']/quotas/quota/@galaxy">18</replace>
let product_replace_xml (id: string) (quota_type: string) (quota: int) =
    let xml =
        new XElement(
            "replace",
            new XAttribute("sel", $"//god/products/product[@id='{id}']/quotas/quota/@{quota_type}"),
            quota
        )

    printfn "  REPLACING PRODUCT %s with quota %s:%i using:\n %s" id quota_type quota (xml.ToString())
    xml

// "products" define the number of production modules that will be created for a faction, scattered
// between their factories. We're going tp increase it for Xenon, and reduce it for other major factions.
let processProduct (product: X4WorldStart.Product) =
    logProduct product

    match product.Owner, product.Ware with
    | "xenon", _ -> Some(product_replace_xml product.Id "galaxy" (product.Quota.Galaxy * Economy.XenonProductionRatio))
    | "khaak", _
    | "yaki", _
    | "scaleplate", _
    | "buccaneers", _
    | "player", _ -> None // These are all fine as is.
    //| "terran", "energycells" -> Since we're moving TER back to earth, they no longer need this boost.
    // The sectors we've assigned Terra have low solar output, so they're already crippled.
    // We won't reduce production, but we will increase the limit per sector so that they
    // can spawn all their factories in the slightly less bad .4 sunlight sector.
    //    Some (product_replace_xml product.Id "sector" ( Option.defaultValue 32 product.Quota.Sector * 2) )
    | _ ->
        let reducedQuota =
            (float product.Quota.Galaxy) * Economy.ProductionRatio |> ceil |> int
        // X4 9.0 added static 'prefab' factories for these factions, which we keep (they
        // spawn inside the faction's own shrunk territory). They're additive to product
        // quotas, so subtract the prefab factory count for this ware from the reduced
        // quota - keeping the faction's factory total at the intended weakened level.
        let prefabs =
            prefabFactoryCounts
            |> Map.tryFind (product.Owner, product.Ware)
            |> Option.defaultValue 0

        let newGalaxyQuota = max Economy.MinimumProductQuota (reducedQuota - prefabs)

        if prefabs > 0 then
            printfn "  PREFAB OFFSET %s:%s quota %i -> %i (%i prefab factories)" product.Owner product.Ware reducedQuota newGalaxyQuota prefabs

        Some(product_replace_xml product.Id "galaxy" newGalaxyQuota) // Everyone else gets half the quota.


// Ok, now to generate the new defense stations that we need around each unsage gate.
// X4Gates.getRequiredDefenseStationLocations will do the work of finding the gates and
// calculating the locations for us, and returning a list that tells us which gate, faction
// and locatio to place the station.
// We need to use that information to find an existing defense station for the faction, and
// then copy it, placing it in to the same zone as the gate (which we have stored in the gate object)
// ==== BASTION CONSTRUCTION PLANS ====
// The bastion stations we place at the gates used to inherit each faction's standard
// defence station construction plan (via the cloned god entry's <select> tag). With
// 9.0's more dangerous Xenon capital ships they were getting pummelled, so we now
// generate beefed up 'bastion' plans instead: GateDefence.BastionStrengthMultiplier
// copies of the faction's vanilla defence plan stacked vertically into one station.

// The vanilla defence construction plan each faction's god '<select>' would resolve to
// (via stations.xml -> stationgroups.xml), the constructionplans file that defines it,
// and the DLC (extension id, name) providing it, if any.
let bastionPlanSources =
    let core = X4UnpackedDataFolder + "/libraries/constructionplans.xml"

    let dlc (extension: string) =
        X4UnpackedDataFolder + "/extensions/" + extension + "/libraries/constructionplans.xml"

    Map [
        "argon", ("arg_defence", core, None)
        "antigone", ("arg_defence", core, None)
        "hatikvah", ("arg_defence", core, None)
        "paranid", ("par_defence", core, None)
        "holyorder", ("par_defence", core, None)
        "teladi", ("tel_defence", core, None)
        "ministry", ("tel_defence", core, None)
        "split", ("spl_defence", dlc "ego_dlc_split", Some("ego_dlc_split", "Split Vendetta"))
        "freesplit", ("spl_defence", dlc "ego_dlc_split", Some("ego_dlc_split", "Split Vendetta"))
        "terran", ("ter_defence", dlc "ego_dlc_terran", Some("ego_dlc_terran", "Cradle of Humanity"))
        "pioneers", ("pio_defence", dlc "ego_dlc_terran", Some("ego_dlc_terran", "Cradle of Humanity"))
        "boron", ("bor_defence", dlc "ego_dlc_boron", Some("ego_dlc_boron", "Kingdom End"))
        "loanshark", ("vig_defence", dlc "ego_dlc_pirate", Some("ego_dlc_pirate", "Tides of Avarice"))
        "scavenger", ("rip_defence", dlc "ego_dlc_pirate", Some("ego_dlc_pirate", "Tides of Avarice"))
    ]

// The id of the generated bastion plan a faction's gate stations will reference.
// Shared between factions that use the same vanilla defence plan.
let bastionPlanId (faction: string) =
    match bastionPlanSources |> Map.tryFind faction with
    | Some(planId, _, _) -> $"atf_bastion_{planId}"
    | None -> failwithf "No defence construction plan mapping for faction '%s': add it to bastionPlanSources in god.fs" faction

// The physical vertical extent of a station module, in metres (below anchor, above
// anchor). A plan entry's offset position is only the module's ANCHOR point - the
// module body extends beyond it (e.g. the argon claim module reaches 586m below and
// 708m above its anchor). We bound the body using every connection offset on the
// module's component: turrets, shields and docks sit on the hull surface, so they give
// a good lower bound on the real mesh extent (which lives in binary files we can't read).
let moduleVerticalExtent =
    let cache = System.Collections.Generic.Dictionary<string, float * float>()

    fun (macroName: string) ->
        match cache.TryGetValue macroName with
        | true, extent -> extent
        | _ ->
            let entry =
                match AllIndexMacros |> Array.tryFind (fun e -> e.Name =? macroName) with
                | Some entry -> entry
                | None -> failwithf "Bastion plan module macro '%s' not found in the macro index" macroName

            let macroDoc =
                XDocument.Load(X4UnpackedDataFolder + "/" + entry.File.Replace("\\", "/"))

            let componentRef =
                (macroDoc.Descendants(XName.Get "component") |> Seq.head)
                    .Attribute(XName.Get "ref")
                    .Value

            let componentEntry =
                match AllComponentMacros |> Array.tryFind (fun e -> e.Name =? componentRef) with
                | Some entry -> entry
                | None -> failwithf "Component '%s' of module '%s' not found in the component index" componentRef macroName

            let componentDoc =
                XDocument.Load(X4UnpackedDataFolder + "/" + componentEntry.File.Replace("\\", "/"))

            // NOTE: because this list uses an explicit 'yield' in the loop, the first
            // element must be explicitly yielded too - a bare '0.0' would be discarded.
            let connectionYs = [
                yield 0.0 // the anchor itself, so a module with no offset connections gets extent (0,0)
                for connection in componentDoc.Descendants(XName.Get "connection") do
                    match connection.Element(XName.Get "offset") with
                    | null -> ()
                    | offset ->
                        match offset.Element(XName.Get "position") with
                        | null -> ()
                        | position ->
                            match position.Attribute(XName.Get "y") with
                            | null -> ()
                            | y -> yield float y.Value
            ]

            let extent = (-(List.min connectionYs), List.max connectionYs)
            cache.[macroName] <- extent
            extent

// Build a stacked bastion plan from a vanilla defence plan: duplicate the full entry
// list for each extra copy, remapping the entry indices and predecessor references of
// the duplicates, and lifting each copy vertically so the copies don't overlap. The
// first entry of each copy has no predecessor, so the game places it at its absolute
// offset - the same mechanism vanilla uses for entry 1 of every plan.
// The lift is the plan's PHYSICAL vertical span (anchor positions extended by each
// module's body) plus BastionCopyClearance. Defence plans range from ~1300m tall (arg,
// whose anchors all sit on one plane) to ~3000m (tel), so a fixed lift would leave
// some designs clipping and others needlessly far apart.
let makeBastionPlan (sourcePlan: XElement) (newId: string) (dlcPatch: (string * string) option) =
    let plan = new XElement(sourcePlan)
    plan.SetAttributeValue(XName.Get "id", newId)
    plan.SetAttributeValue(XName.Get "name", "Bastion")

    let entries = plan.Elements(XName.Get "entry") |> Seq.toList
    let entryCount = entries.Length

    // The anchor position of an entry within the plan.
    let entryY (entry: XElement) =
        match entry.Element(XName.Get "offset") with
        | null -> 0.0
        | offset ->
            match offset.Element(XName.Get "position") with
            | null -> 0.0
            | position ->
                match position.Attribute(XName.Get "y") with
                | null -> 0.0
                | y -> float y.Value

    // Physical top and bottom of the whole station: every module's anchor extended by
    // that module's body extent.
    let bounds =
        entries
        |> List.map (fun entry ->
            let y = entryY entry
            let below, above = moduleVerticalExtent (entry.Attribute(XName.Get "macro").Value)
            y - below, y + above)

    let physicalBottom = bounds |> List.map fst |> List.min
    let physicalTop = bounds |> List.map snd |> List.max
    let physicalSpan = physicalTop - physicalBottom

    printfn "    physical span %.0fm (%.0f .. %.0f), lifting copies by %.0fm" physicalSpan physicalBottom physicalTop (physicalSpan + GateDefence.BastionCopyClearance)

    for copy in 1 .. GateDefence.BastionStrengthMultiplier - 1 do
        let lift = (physicalSpan + GateDefence.BastionCopyClearance) * float copy

        for entry in entries do
            let clone = new XElement(entry)
            let index = int (clone.Attribute(XName.Get "index").Value)
            clone.SetAttributeValue(XName.Get "index", index + entryCount * copy)

            match clone.Element(XName.Get "predecessor") with
            | null -> ()
            | predecessor ->
                let predIndex = int (predecessor.Attribute(XName.Get "index").Value)
                predecessor.SetAttributeValue(XName.Get "index", predIndex + entryCount * copy)

            // Lift the whole copy vertically. Every offset position in the plan is in
            // absolute station coordinates, so shift them all uniformly.
            let offset =
                match clone.Element(XName.Get "offset") with
                | null ->
                    let o = new XElement(XName.Get "offset")
                    clone.AddFirst o
                    o
                | o -> o

            let position =
                match offset.Element(XName.Get "position") with
                | null ->
                    let p = new XElement(XName.Get "position")
                    offset.AddFirst p
                    p
                | p -> p

            let currentY =
                match position.Attribute(XName.Get "y") with
                | null -> 0.0
                | y -> float y.Value

            position.SetAttributeValue(XName.Get "y", currentY + lift)

            plan.Add(clone)
            plan.Add(new XText("\n"))

    // Declare the DLC the plan's modules come from, mirroring the pattern of the hand
    // written plans in the mod_xml constructionplans template.
    match dlcPatch with
    | Some(extension, name) ->
        let patch = new XElement(XName.Get "patch")
        patch.SetAttributeValue(XName.Get "extension", extension)
        patch.SetAttributeValue(XName.Get "version", "700")
        patch.SetAttributeValue(XName.Get "name", name)
        let patches = new XElement(XName.Get "patches")
        patches.Add patch
        plan.AddFirst patches
    | None -> ()

    plan

// Generate the bastion construction plans for the given factions and write the mod's
// constructionplans.xml. Seeded from the hand written template (which holds e.g. the
// scrap cross plan) because our write replaces the copy Program.fs made of it.
let generateBastionConstructionPlans (factions: string list) =
    printfn "======= Generating bastion construction plans"

    let plansOut =
        XElement.Load(new XmlTextReader(__SOURCE_DIRECTORY__ + "/mod_xml/libraries/constructionplans.xml"))

    factions
    |> List.map (fun faction ->
        match bastionPlanSources |> Map.tryFind faction with
        | Some source -> source
        | None -> failwithf "No defence construction plan mapping for faction '%s': add it to bastionPlanSources in god.fs" faction)
    |> List.distinct
    |> List.iter (fun (planId, file, dlcPatch) ->
        let sourcePlan =
            XDocument.Load(file).Descendants(XName.Get "plan") // DLC files may be diffs, so search all descendants
            |> Seq.tryFind (fun p -> p.Attribute(XName.Get "id").Value = planId)
            |> Option.defaultWith (fun () -> failwithf "Defence construction plan '%s' not found in %s" planId file)

        printfn "  BASTION PLAN atf_bastion_%s (%ix %s)" planId GateDefence.BastionStrengthMultiplier planId
        plansOut.Add(makeBastionPlan sourcePlan $"atf_bastion_{planId}" dlcPatch)
        plansOut.Add(new XText("\n")))

    WriteModfiles.write_xml_file "core" "libraries/constructionplans.xml" plansOut

// and then updating the location to the new coordinates, and finally renaming the station to be
// '(gate.name).defense_[id]' so that it's unique.
let generateGateDefenseStations () =
    let gateStations =
        X4.Gates.getRequiredDefenseStationLocations GateDefence.StationsPerGate GateDefence.StationDistance

    // Generate the beefed up bastion construction plans for every faction that gets
    // gate stations, and write the mod's constructionplans.xml.
    gateStations
    |> List.map (fun (gate, _, _) -> gate.Faction)
    |> List.distinct
    |> generateBastionConstructionPlans

    [
        for gate, n, location in gateStations do
            printfn "GENERATING DEFENSE STATION FOR %s GATE %s" gate.Faction gate.ConnectionName
            // find the first defence station for the faction. We want to fail if we find nothing, as that would break the mod.
            let station =
                match findStation gate.Faction "defence" allStations with
                | Some station -> station
                | None -> failwithf "No defense station found for faction %s" gate.Faction

            printfn "  FOUND DEFENSE STATION %s owner:%s" station.Id station.Owner

            let stationClone = new XElement(station.XElement)
            let defenseStation = new X4GodMod.Station(stationClone)
            defenseStation.XElement.SetAttributeValue(XName.Get("id"), gate.ConnectionName + "_bastion_" + n.ToString()) // Give it a new unique ID

            // Point the bastion at our stacked construction plan instead of letting the
            // game resolve the faction's standard defence plan through the <select> tag.
            let stationSpec =
                match defenseStation.XElement.Element(XName.Get "station") with
                | null ->
                    let spec = new XElement(XName.Get "station")
                    defenseStation.XElement.Add spec
                    spec
                | spec -> spec

            match stationSpec.Element(XName.Get "select") with
            | null -> ()
            | select -> select.Remove()

            stationSpec.SetAttributeValue(XName.Get "constructionplan", bastionPlanId gate.Faction)

            // update location and set the location of the station copy to be the zone of the gate,
            let zone = gate.X4Zone
            defenseStation.Location.XElement.SetAttributeValue(XName.Get("class"), zone.Class)
            defenseStation.Location.XElement.SetAttributeValue(XName.Get("macro"), zone.Name)
            defenseStation.Location.XElement.SetAttributeValue(XName.Get("matchextension"), "false") // without this, game ignores mods touching things outside their scope
            defenseStation.Location.XElement.SetAttributeValue("solitary", null) // VIG faction has this attribute set that may cause station placement to fail

            // Now update the precise position within the zone to the caculated spot in a circle near the gate.
            let position =
                match defenseStation.Position with
                | Some position -> position.XElement
                | None ->
                    printfn "   No position found for station %s" defenseStation.Id
                    let posXml = new XElement("position")
                    defenseStation.XElement.Add(posXml)
                    defenseStation.XElement.Add(new XText("\n")) // Add a newline after each element so the output is readible
                    posXml

            position.SetAttributeValue(XName.Get("x"), location.X)
            position.SetAttributeValue(XName.Get("y"), location.Y)
            position.SetAttributeValue(XName.Get("z"), location.Z)

            logAddStation "ADDING" defenseStation
            defenseStation.XElement // return an add/remove options,
    ]


// There are several new xenon stations we want to add to specific sectors defined in 'Data.newXenonStations'.
// Here we create the XML oibjects that represent these in the approriate location.
// There are several new xenon stations we want to add to specific sectors defined in 'Data.newXenonStations'.
// Here we create the XML oibjects that represent these in the approriate location.
let addNewXenonStations (xenonShipyard: XElement) (xenonWharf: XElement) =
    Territories.newXenonStations
    |> List.choose (fun xenonStation ->
        match xenonStation with
        | XenonShipyard(locClass, location) ->
            Some(new X4GodMod.Station(new XElement(xenonShipyard)), locClass, location)
        | XenonWharf(locClass, location) -> Some(new X4GodMod.Station(new XElement(xenonWharf)), locClass, location)
        | XenonSolarStation _ -> None)
    |> List.map (fun (station, locClass, location) ->
        // Update the location and ID of our new station.
        station.Location.XElement.SetAttributeValue(XName.Get("class"), locClass)
        station.Location.XElement.SetAttributeValue(XName.Get("macro"), location)
        station.XElement.SetAttributeValue(XName.Get("id"), station.Id + location) // Give it a new unique ID
        logAddStation "ADDING" station
        station.XElement)

// While defense stations are predefined; Xenon solar stations are not. It's quite possible that we could
// create a concept of solar stations like defense/wharf/etc, that seems like more work. Instead we'll simply
// define a new product with a location of the sector, and a quota of 1 for each instance.
let addNewXenonProducts () =
    Territories.newXenonStations
    |> List.choose (fun xenonStation ->
        match xenonStation with
        | XenonSolarStation(locClass, location) -> Some(locClass, location)
        | _ -> None)
    |> List.map (fun (locClass, location) ->
        let id = "xen_solar_" + location

        let product =
            new XElement(
                "product",
                new XAttribute("id", id),
                new XAttribute("ware", "energycells"),
                new XAttribute("owner", "xenon"),
                new XAttribute("type", "factory"),
                new XText("\n"),
                new XElement("quotas", new XElement("quota", new XAttribute("galaxy", 2), new XAttribute("sector", 2))),
                new XText("\n"),
                new XElement(
                    "location",
                    new XAttribute("class", locClass),
                    new XAttribute("macro", location),
                    new XAttribute("matchextension", "false")
                ),
                new XText("\n"),
                new XElement(
                    "module",
                    new XElement("select", new XAttribute("ware", "energycells"), new XAttribute("race", "xenon"))
                ),
                new XText("\n")
            )

        printfn "   ADDING PRODUCT xen_solar to %s:%s" locClass location
        product)

// Process the GOD file from the core game, and the DLCs.
// extract the stations and products, tweak some values, then write out a new GOD file.
let generate_god_file (filename: string) =
    // Extract the Xenon stations from the GodModTemplate. We'll use these as templates when we add new xenon stations
    let X4ObjectTemplatesData = X4ObjectTemplates.Load(X4ObjectTemplatesFile)

    let xenonShipyard =
        (Array.find
            (fun (elem: X4ObjectTemplates.Station) -> elem.Id = "shipyard_xenon_cluster")
            X4ObjectTemplatesData.Stations)
            .XElement

    let xenonWharf =
        (Array.find
            (fun (elem: X4ObjectTemplates.Station) -> elem.Id = "wharf_xenon_cluster")
            X4ObjectTemplatesData.Stations)
            .XElement

    let xenonDefence =
        (Array.find
            (fun (elem: X4ObjectTemplates.Station) -> elem.Id = "xen_defence_cluster")
            X4ObjectTemplatesData.Stations)
            .XElement

    let stationsToMove =
        findStationsThatNeedMoving allStations |> List.map (fun s -> s.Id) // find the IDs of the stations we're going to move to a safe sector

    let (addStations, removeStations, moveStations) =
        [
            for station in allStations do
                yield processStation station stationsToMove xenonShipyard xenonWharf xenonDefence
        ]
        |> splitTuples

    let replaceProducts = [
        for product in allProducts do
            match processProduct product with
            | Some product -> yield product
            | _ -> ()
    ]

    let newDefenseStations = generateGateDefenseStations ()
    let newXenonStations = addNewXenonStations xenonShipyard xenonWharf
    let newXenonProducts = addNewXenonProducts ()


    // Now that everything has been processed, and we've got new stations and products,
    // we generate the modded XML, and write it out.

    // This is our template output file structure, with the broad sections already created.
    // We have set up the 'add' 'replace' sections with appropriate selectors. We can then
    // extract each Add section by the selector element value, and populate it's underlying
    // XElement in place. eg, search for "//god/stations" to find the 'add' XElement for
    // stations.
    let outGodFile =
        X4GodMod.Parse(
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>
        <diff>
            <add sel=\"//god/stations\">
            </add>
            <add sel=\"//god/products\">
            </add>
        </diff>
    "
        )

    let stationsAddElem = find_add_selector "//god/stations" outGodFile.Adds
    let productsAddElem = find_add_selector "//god/products" outGodFile.Adds

    // The stations we're replacing with Xenon.
    [
        for element in addStations do
            stationsAddElem.XElement.Add(element)
            stationsAddElem.XElement.Add(new XText("\n")) // Add a newline after each element so the output is readible
    ]
    |> ignore

    // The new defense stations we're adding near gates.
    [
        for element in newDefenseStations do
            stationsAddElem.XElement.Add(element)
            stationsAddElem.XElement.Add(new XText("\n")) // Add a newline after each element so the output is readible
    ]
    |> ignore

    // The handful of specific new extra Xenon worfsa/shipyards we're adding to apply preassure in specific sectors.
    [
        for element in newXenonStations do
            stationsAddElem.XElement.Add(element)
            stationsAddElem.XElement.Add(new XText("\n")) // Add a newline after each element so the output is readible
    ]
    |> ignore

    [
        for element in newXenonProducts do
            productsAddElem.XElement.Add(element)
            productsAddElem.XElement.Add(new XText("\n")) // Add a newline after each element so the output is readible
    ]
    |> ignore



    // Add out 'remove' tags to the end of the diff block.
    let diff = outGodFile.XElement // the root element is actually the 'diff' tag.

    let changes =
        List.concat [ removeStations; replaceProducts; (List.concat moveStations) ]

    [
        for element in changes do
            diff.Add(element)
            diff.Add(new XText("\n")) // Add a newline after each element so the output is readible
    ]
    |> ignore

    WriteModfiles.write_xml_file "core" filename outGodFile.XElement
