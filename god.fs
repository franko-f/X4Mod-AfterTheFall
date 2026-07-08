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

open X4.Types
open X4.Utilities
open X4.Data
open X4.Territories

open X4.Tuning // Economy ratios and gate defence values live in tuning.fs


// the 'log' functions just extract a bit of data about a station, and log it
// to the terminal for debugging and tracking purposes.
let logStation (action: string) (station: GodStation) =
    let tags = station.SelectTags |> Option.defaultValue "[none]"

    printfn
        "%s STATION %s race: %A, owner: %A, type: %A, location: %A:%A, id: %A, station: %A   "
        action
        tags
        station.Race
        station.Owner
        station.Type
        station.LocationClass
        station.LocationMacro
        station.Id
        station.StationMacro


let logProduct (product: GodProduct) =
    printfn
        "PROCESSING PRODUCT [%s:%s] %s/%s with quotas %i/%i"
        product.Owner
        (product.LocationFaction |> Option.defaultValue "UNKNOWN")
        product.Type
        product.Ware
        product.QuotaGalaxy
        (product.QuotaSector |> Option.defaultValue -1)



let stationSectorName (station: GodStation) =
    match station.LocationClass with
    | Some "zone" ->
        findSectorFromZone (station.LocationMacro |> Option.defaultValue "")
        |> Option.defaultValue "none"
    | Some "sector" -> station.LocationMacro |> Option.defaultValue "none"
    | _ -> "none"

// Station owners the mod never touches: the enemies whose stations we're clearing
// space for, and special factions with no territory of their own.
let private ignoredOwners = [
    "khaak"
    "xenon"
    "yaki"
    "scaleplate"
    "buccaneers"
    "player"
    "kaori"
    "holyorderfanatic"
]

// The major factions: a station inside any of their (shrunk) territories survives.
let private majorFactions = [
    "teladi"
    "paranid"
    "holyorder"
    "split"
    "freesplit"
    "argon"
    "antigone"
    "hatikvah"
    "ministry"
]

// Is this station in a sector we're going to leave alone? ie, in the territory of the owning faction
// or a faction we're ignoring and not changing?
let ignoreStation (station: GodStation) =
    let sector = stationSectorName station
    let inTerritory = isFactionInSector station.Owner sector // Station already in the territory of the owning faction

    let inFriendlyTerritory =
        majorFactions |> List.exists (fun faction -> isFactionInSector faction sector)

    let isIgnored = List.contains station.Owner ignoredOwners

    let isPirateBase =
        station.SelectTags |> Option.exists (fun tags -> tags = "[piratebase]")

    inTerritory || isIgnored || isPirateBase || inFriendlyTerritory


// Find and return the first occurrence of a station with the given faction and type.
// Used to find things like faction defence stations, wharfs, etc, so that we can move
// the first instance to the factions 'safe' location, while removing the rest.
// Sometimes the station type is determined by the value of 'station.type', but other times,
// station.type is set to 'factory', and you must look to the 'tags' field to determine the
// type of station.
let findStation (faction: string) (stationType: string) (stations: GodStation list) =
    let ofFaction (predicate: GodStation -> bool) =
        stations |> List.tryFind (fun station -> station.Owner = faction && predicate station)

    // First by explicit type; then by the select tags (station.type is often just
    // 'factory'); and some terran/PIO defence stations have neither - they're only
    // identifiable by their construction plan.
    ofFaction (fun station -> station.Type = Some stationType)
    |> Option.orElseWith (fun () ->
        ofFaction (fun station -> station.SelectTags |> Option.exists (fun tags -> tags.Contains stationType)))
    |> Option.orElseWith (fun () ->
        ofFaction (fun station -> station.ConstructionPlan |> Option.exists (fun plan -> plan.Contains stationType)))


// MAIN PROCESSING FUNCTIONS

// Given a station, decide what happens to it according to our rules: which Xenon
// station (if any) replaces it, and whether the original is removed or moved to a
// safe sector. Returns None for stations we leave completely alone.
// stationsToMove are the IDs of stations that we're going to move to a safe sector,
// rather than completely replace by a Xenon one. We'll still put a xenon station
// where they used to be.
let processStation (station: GodStation) (stationsToMove: string list) : StationDirective option =
    logStation "PROCESSING" station

    match ignoreStation station with
    | true ->
        // This station is in a sector we're leaving alone..
        printfn "  LEAVING [%s]:%s :: %A" station.Owner (stationSectorName station) station.Id
        None
    | _ ->
        // 'SelectTags' describe whether this is a defence station, wharf or shipyard,
        // which determines the kind of Xenon station that replaces it.
        let replacementKind =
            match station.SelectTags, station.Type with
            | (None, Some "tradingstation") ->
                // These seem to be teladi tranding stations. Replace with something more... interesting
                Some XenonWharfStation
            | (None, Some "factory") ->
                // MOST examples in the logs without a tag all seem to be scenarios we weren't going to replace. Khaak, xenon, etc.
                // But TERRAN/SEG has a few defence stations without tags. We'll check their construction plan instead.
                match station.ConstructionPlan with
                | Some "'ter_defence'" -> Some XenonDefenceStation
                | Some "'ter_defenceplatform'" -> Some XenonDefenceStation
                | Some "'pio_defence'" -> Some XenonDefenceStation
                | _ -> None

            | (None, _) ->
                // the other examples in the logs without a tag all seem to be scenarios we weren't going to replace. Khaak, xenon, etc.
                None
            | (Some tags, _) ->
                // We're going to replace different types of NPC buildings with different Xenon stations.
                match tags with
                | "[shipyard]" -> Some XenonShipyardStation
                | "[wharf]"
                | "[equipmentdock]" -> Some XenonWharfStation
                | "[defence]"
                | "[tradestation]"
                | "[piratebase]" -> Some XenonDefenceStation // For now, replace HAT piratebase with xenon defense.
                | x ->
                    printfn "UNHANDLED STATION TYPE: %s - DEFAULTING TO XENON DEFENCE" x
                    Some XenonDefenceStation

        match replacementKind with
        | None ->
            printfn "  IGNORING DEFAULT [%s]" station.Owner // These will still exist, and probably get wiped pretty quick, unless they're well hidden.
            None

        | Some kind ->
            // ok, we're going to replace this station with a Xenon one - unless it sits
            // in a neutral cluster, in which case we clear it without a replacement.
            let locationClass = Option.defaultValue "none" station.LocationClass
            let locationMacro = Option.defaultValue "none" station.LocationMacro

            let cluster =
                findClusterFromLocation locationClass locationMacro
                |> Option.defaultValue "none" // Find out which cluster this location is in.

            let replacement =
                match X4.Territories.neutralClusters |> List.contains cluster with
                | true -> None // Neutral cluster: clear all stations out of it, no xenon replacement.
                | false -> Some kind

            // Now decide whether to REMOVE or MOVE the old station.
            let action =
                if List.contains station.Id stationsToMove then
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

                    MoveOriginalTo randomSector
                else
                    RemoveOriginal

            Some {
                Original = station
                Replacement = replacement
                Action = action
            }


// with the shifting around of valid territory, the various races have lost some of their critical wharfs and shipyards.
// We need to re-add some, but not all. Just make sure each faction has at least one of each type.
let findStationsThatNeedMoving (stations: GodStation list) =
    stations
    // We're looking for the stations we did NOT ignore earlier: ie, the ones that may
    // have been replaced. (ignoreStation already excludes every ignoredOwners faction.)
    |> List.filter (fun (station: GodStation) -> ignoreStation station = false)
    // And we're only interested in the ones that are shipyards, wharfs, trading stations, etc.
    |> List.filter (fun station ->
        // most special stations are identified by tags in the optional 'select' field.
        station.SelectTags
        |> Option.exists (fun tags ->
            List.contains tags [ "[shipyard]"; "[wharf]"; "[equipmentdock]"; "[tradestation]" ])
        || station.Type = Some "tradingstation" // Teladi trading stations are identified differently, by using type.
    )
    // Deduplication rules (deliberate, decided 2026-07 - see git history for the T1 story):
    // - Stations WITH a <select> tag are each treated as unique infrastructure and are
    //   ALL moved to safety, even when a faction has several of the same kind (e.g. both
    //   argon trade stations, both split equipment docks). Keying on Source (which
    //   compares by the identity of the underlying parsed element) makes every such
    //   station distinct.
    // - Only tag-less stations (the teladi 'tradingstation' type) dedupe by (Owner, Type),
    //   so just the first of those is moved and the rest are removed.
    // Historical note: this started as an accident (the old code compared provider values
    // by reference where value equality was probably intended), but we've chosen to keep
    // it: factions hold on to their unique stations, which suits the mod's balance.
    |> List.distinctBy (fun station ->
        (station.Owner, station.Type, station.SelectTags |> Option.map (fun _ -> station.Source)))



// "products" define the number of production modules that will be created for a faction, scattered
// between their factories. We're going tp increase it for Xenon, and reduce it for other major factions.
let processProduct (product: GodProduct) : ProductDirective option =
    logProduct product

    let setGalaxyQuota quota = Some {
        ProductId = product.Id
        QuotaType = "galaxy"
        Quota = quota
    }

    match product.Owner, product.Ware with
    | "xenon", _ -> setGalaxyQuota (product.QuotaGalaxy * Economy.XenonProductionRatio)
    | "khaak", _
    | "yaki", _
    | "scaleplate", _
    | "buccaneers", _
    | "player", _ -> None // These are all fine as is.
    | _ ->
        let reducedQuota =
            (float product.QuotaGalaxy) * Economy.ProductionRatio |> ceil |> int
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

        setGalaxyQuota newGalaxyQuota // Everyone else gets a fraction of the quota.


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
// (via stations.xml -> stationgroups.xml), and the DLC (extension id, name) providing
// it, if any. The data layer (X4.Data.Plans.loadDefencePlan) resolves the actual file.
let bastionPlanSources =
    Map [
        "argon", { PlanId = "arg_defence"; Dlc = None }
        "antigone", { PlanId = "arg_defence"; Dlc = None }
        "hatikvah", { PlanId = "arg_defence"; Dlc = None }
        "paranid", { PlanId = "par_defence"; Dlc = None }
        "holyorder", { PlanId = "par_defence"; Dlc = None }
        "teladi", { PlanId = "tel_defence"; Dlc = None }
        "ministry", { PlanId = "tel_defence"; Dlc = None }
        "split", { PlanId = "spl_defence"; Dlc = Some("ego_dlc_split", "Split Vendetta") }
        "freesplit", { PlanId = "spl_defence"; Dlc = Some("ego_dlc_split", "Split Vendetta") }
        "terran", { PlanId = "ter_defence"; Dlc = Some("ego_dlc_terran", "Cradle of Humanity") }
        "pioneers", { PlanId = "pio_defence"; Dlc = Some("ego_dlc_terran", "Cradle of Humanity") }
        "boron", { PlanId = "bor_defence"; Dlc = Some("ego_dlc_boron", "Kingdom End") }
        "loanshark", { PlanId = "vig_defence"; Dlc = Some("ego_dlc_pirate", "Tides of Avarice") }
        "scavenger", { PlanId = "rip_defence"; Dlc = Some("ego_dlc_pirate", "Tides of Avarice") }
    ]

// Only hatikvah bastions get an explicit <loadout> level; every other faction
// inherits whatever its cloned god entry carried. Hatikvah needs the help twice
// over: their vanilla defence entry has no loadout level (near-unarmed default
// fit), and their wares.xml ownership contains a single L combat turret (the
// pulse laser) - see Tuning.GateDefence.FactionWareGrants for the ware fix that
// lets the level actually mount plasma.
let bastionLoadoutLevel (faction: string) =
    if faction = "hatikvah" then Some X4.Tuning.GateDefence.BastionLoadoutLevel else None

// The id of the generated bastion plan a faction's gate stations will reference.
// Shared between factions that use the same vanilla defence plan.
let bastionPlanId (faction: string) =
    match bastionPlanSources |> Map.tryFind faction with
    | Some source -> $"atf_bastion_{source.PlanId}"
    | None -> failwithf "No defence construction plan mapping for faction '%s': add it to bastionPlanSources in god.fs" faction

// Decide the bastion stations for every unsafe gate, and the stacked construction
// plans they build. The data layer renders and writes both.
let gateBastions () =
    let gateStations =
        X4.Gates.getRequiredDefenseStationLocations GateDefence.StationsPerGate GateDefence.StationDistance

    // The beefed up bastion construction plans for every faction that gets gate stations.
    let planDirectives =
        gateStations
        |> List.map (fun (gate, _, _) -> gate.Faction)
        |> List.distinct
        |> List.map (fun faction ->
            match bastionPlanSources |> Map.tryFind faction with
            | Some source -> source
            | None -> failwithf "No defence construction plan mapping for faction '%s': add it to bastionPlanSources in god.fs" faction)
        |> List.distinct // pure record distinct: factions sharing a defence plan produce it once
        |> List.map (fun source -> {
            NewId = $"atf_bastion_{source.PlanId}"
            SourcePlanId = source.PlanId
            SourcePlan = loadDefencePlan source
            DlcPatch = source.Dlc
        })

    // One bastion station per calculated gate ring position, based on the faction's
    // own defence station god entry.
    let bastionStations = [
        for gate, n, location in gateStations do
            printfn "GENERATING DEFENSE STATION FOR %s GATE %s" gate.Faction gate.ConnectionName
            // find the first defence station for the faction. We want to fail if we find nothing, as that would break the mod.
            let station =
                match findStation gate.Faction "defence" allStations with
                | Some station -> station
                | None -> failwithf "No defense station found for faction %s" gate.Faction

            printfn "  FOUND DEFENSE STATION %s owner:%s" station.Id station.Owner

            {
                BasedOn = station
                Id = gate.ConnectionName + "_bastion_" + n.ToString()
                ZoneClass = gate.GateType
                ZoneName = gate.Zone
                Position = location
                PlanId = bastionPlanId gate.Faction
                LoadoutLevel = bastionLoadoutLevel gate.Faction
            }
    ]

    planDirectives, bastionStations

// There are several new xenon stations we want to add to specific sectors defined in
// 'Territories.newXenonStations'. Split them into station and solar-product directives.
let newXenonStationDirectives =
    Territories.newXenonStations
    |> List.choose (fun xenonStation ->
        match xenonStation with
        | XenonShipyard(locClass, location) ->
            Some {
                Kind = XenonShipyardStation
                LocationClass = locClass
                LocationMacro = location
            }
        | XenonWharf(locClass, location) ->
            Some {
                Kind = XenonWharfStation
                LocationClass = locClass
                LocationMacro = location
            }
        | XenonSolarStation _ -> None)

// While defense stations are predefined; Xenon solar stations are not. It's quite possible that we could
// create a concept of solar stations like defense/wharf/etc, that seems like more work. Instead we'll simply
// define a new product with a location of the sector, and a quota of 1 for each instance.
let newXenonSolarProducts =
    Territories.newXenonStations
    |> List.choose (fun xenonStation ->
        match xenonStation with
        | XenonSolarStation(locClass, location) ->
            Some {
                LocationClass = locClass
                LocationMacro = location
            }
        | _ -> None)

// Process the GOD file from the core game, and the DLCs: decide the fate of every
// station and product, plus the new bastions and Xenon stations, then hand all the
// directives to the data layer to write god.xml and constructionplans.xml.
let generate_god_file (filename: string) =
    let stationsToMove =
        findStationsThatNeedMoving allStations |> List.map (fun s -> s.Id) // find the IDs of the stations we're going to move to a safe sector

    let stationDirectives =
        allStations
        |> List.choose (fun station -> processStation station stationsToMove)

    let productDirectives = allProducts |> List.choose processProduct

    let bastionPlans, bastionStations = gateBastions ()
    writeConstructionPlans bastionPlans

    writeGodFile filename stationDirectives bastionStations newXenonStationDirectives newXenonSolarProducts productDirectives
