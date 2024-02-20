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

open System.Xml.Linq
open FSharp.Data
open X4.Utilities
//open X4.Data

[<Literal>]
let X4GodFileCore = X4.Data.X4UnpackedDataFolder + "/core/libraries/god.xml" // Core game data.
let X4GodFileSplit = X4.Data.X4UnpackedDataFolder + "/split/libraries/god.xml" // Core game data.
let X4GodFileTerran = X4.Data.X4UnpackedDataFolder + "/terran/libraries/god.xml" // Core game data.
let X4GodFilePirate = X4.Data.X4UnpackedDataFolder + "/pirate/libraries/god.xml" // Core game data.
let X4GodFileBoron = X4.Data.X4UnpackedDataFolder + "/boron/libraries/god.xml" // Core game data.


// The default GOD.xml file, etc, don't have an 'add/replace' XML section, so they can't
// be used as type providers for our output XML. So we've created a template that has
// versions of our types that we need (like station, product) as well as the XML selectors
// for replace/add
[<Literal>]
let X4GodModFile = __SOURCE_DIRECTORY__ + "/mod_templates/god.xml"
[<Literal>]
let X4ObjectTemplatesFile = __SOURCE_DIRECTORY__ + "/mod_templates/object_templates.xml"


// TODO: Should we 'unify' all the types using 'Global=true,' parameter?
// This means, for example, every instance of 'Location' type is trested as the same type, no matter
// where it appears in the sample data file. It results in a lot of fields being set to an 'option'
// type, as some will appear in some cases of location, but not in others. So requires some tweaks
// to the parsing code.
// https://fsprojects.github.io/FSharp.Data/library/XmlProvider.html#Global-inference-mode
type X4WorldStart = XmlProvider<X4GodFileCore>
type X4GodMod = XmlProvider<X4GodModFile >
type X4ObjectTemplates = XmlProvider<X4ObjectTemplatesFile>


// the 'log' functions just extract a bit of data about a station, and log it
// to the terminal for debugging and tracking purposes.
let logStation (station:X4WorldStart.Station) =
    let tags         = match station.Station.Select with | Some tag -> tag.Tags | _ -> "[none]"
    printfn "PROCESSING STATION %s race: %A, owner: %A, type: %A, location: %A:%A, id: %A, station: %A   " tags station.Race station.Owner station.Type station.Location.Class station.Location.Macro station.Id station.Station.Macro

let logAddStation (action:string) (station:X4GodMod.Station) =
    let tags         = match station.Station.Select with | Some tag -> tag.Tags | _ -> "[none]"
    printfn "   %s STATION %s race: %s, owner: %s, type: %s, location: %s:%s, id: %s, station: \"none\"   " action tags station.Race station.Owner station.Type station.Location.Class station.Location.Macro station.Id

let logProduct (product:X4WorldStart.Product) =
    printfn "PROCESSING PRODUCT [%s:%s] %s/%s with quotas %i/%i" product.Owner product.Location.Faction product.Type product.Ware product.Quota.Galaxy (product.Quota.Sector |> Option.defaultValue -1)




// Find and return the first occurrence of a station with the given faction and type.
// Used to find things like faction defence stations, wharfs, etc, so that we can move
// the first instance to the factions 'safe' location, while removing the rest.
// Sometimes the station type is determined by the value of 'station.type', but other times,
// station.type is set to 'factory', and you must look to the 'tags' field to determine the
// type of station.
let find_station (faction:string) (stationType:string) (stations:X4WorldStart.Station list) =
    match List.tryFind (fun (station:X4WorldStart.Station) -> station.Owner = faction && station.Type = Some stationType) stations with
    | Some station -> Some station
    | None ->
        // Ok, this might be a case where station type is 'factory', and we need to look at station.station.select.tags
        match List.tryFind (fun (station:X4WorldStart.Station) ->
            // if the station is owned by the correct faction, then attempt to extract the tags by working through
            // the list of option types stored in station.station.select.tags to get to the actual tags (if they exist)
            // then finally check if the tags (a comma separated string) contain the stationType we're looking for.
            station.Owner = faction && station.Station.Select
            |> Option.map (fun x -> x.Tags)
            |> (Option.defaultValue "")
            |> fun tags -> tags.Contains stationType ) stations
        with
        | Some station -> Some station
        | None ->
            // And some terran/PIO defence stations don't have trags either. They use station.constructionplan
            List.tryFind (fun (station:X4WorldStart.Station) ->
                station.Owner = faction && station.Station.Constructionplan
                |> Option.map (fun x -> x)
                |> (Option.defaultValue "")
                |> fun tags -> tags.Contains stationType ) stations


// Extract the stations from the 'add' section of a DLCs god diff/mod file.
// While there are many 'add' sections, we're only interested in the one that
// that has the selectior '//god/stations'
let getStationsFromDiff (diff:X4GodMod.Add[]) = 
    let stationsAdd = Array.filter (fun (add:X4GodMod.Add) -> add.Sel = "/god/stations") diff
    [ for stations in stationsAdd do
            for station in stations.Stations do
                yield new X4WorldStart.Station(station.XElement)
    ]

let getProductFromDiff (diff:X4GodMod.Add[]) = 
    let productsAdd = Array.filter (fun (add:X4GodMod.Add) -> add.Sel = "/god/products") diff
    [ for products in productsAdd do
            for product in products.Products do
                yield new X4WorldStart.Product(product.XElement)
    ]


// Read all thge stations and products from the core game and the DLCs.
let allStations, allProducts =
    let X4GodCore = X4WorldStart.Load(X4GodFileCore)
    let X4GodSplit = X4GodMod.Load(X4GodFileSplit)
    let X4GodTerran = X4GodMod.Load(X4GodFileTerran)
    let X4GodPirate = X4GodMod.Load(X4GodFilePirate)
    let X4GodBoron = X4GodMod.Load(X4GodFileBoron)

    // Finally build up an uberlist of all our stations across all DLC and core game.
    // The DLC stations are of a different type: they're an XML DIFF file, not the GOD
    // file type. So we need to pull out the stations from the diff and convert them
    // to the same type as the core stations using the underlying XElement.
    let allStations =
        List.concat [
            Array.toList X4GodCore.Stations.Stations
            getStationsFromDiff X4GodSplit.Adds
            getStationsFromDiff X4GodTerran.Adds
            getStationsFromDiff X4GodPirate.Adds
            getStationsFromDiff X4GodBoron.Adds
        ]

    // Do the same for products
    let allProducts =
        List.concat [
            Array.toList X4GodCore.Products
            getProductFromDiff X4GodSplit.Adds
            getProductFromDiff X4GodTerran.Adds
            getProductFromDiff X4GodPirate.Adds
            getProductFromDiff X4GodBoron.Adds
        ]

    allStations, allProducts


// Given a selector ID, search an instance of a GodMod xml file for the 'ADD' section
// with that 'sel' value. This will be the XElement we will manipulate.
let find_add_selector sel xml =
   Array.find (fun (elem:X4GodMod.Add) -> elem.Sel = sel) xml


// MAIN PROCESSING FUNCTIONS

// Given a station, process it according to our rules. We may replace it
// with a Xenon one, remove it, etc. This function is call once per station
let processStation (station:X4WorldStart.Station) allSectors (xenonShipyard:XElement) (xenonWharf:XElement) (xenonDefence:XElement)  =
    logStation station

    // So, turns out XmlProvider is more focused around reads. Writes are not... great.
    // To edit underlying fields, you really need to get to the underlying linq XElement
    // and then manipulate that. When trying to create a new XML Provider element based
    // off a copy of an old one, it will use a reference to the underlying XElement.
    // that means our edits will overwrite, sooo we need to clone the XElement each time.

    // First check if the station is within the permitted sector for the faction.
    // We've limited this to just a couple sectors per faction. If it's in one of these
    // we leave it alone. Otherwise, it's going to get... "assimilated"

    let sectorName = 
        match station.Location.Class with
        | Some "zone" -> X4.Data.findSectorFromZone (station.Location.Macro |> Option.defaultValue "") allSectors |> Option.defaultValue "none"
        | Some "sector" -> station.Location.Macro |> Option.defaultValue "none"
        | _ -> "none"
    let inTerritory = (X4.Data.isFactionInSector station.Owner sectorName) || (X4.Data.ignoreFaction station.Owner)

    match inTerritory, station.Owner with
    | true, _ -> 
        // This station is in a sector we're leaving alone..
        printfn "  LEAVING [%s]:%s :: %A" station.Owner sectorName station.Id
        (None, None)
    | _, "khaak" | _, "xenon" | _, "yaki" | _, "scaleplate" | _,"buccaneers" | _, "player" ->
        // We're ignoring certain pirate bases for now like scaleplate and bucconeers. HAT bases we'll still replace below
        printfn "  IGNORING [%s]" station.Owner // These will still exist, and probably get wiped pretty quick, unless they're well hidden.
        (None, None)
    | _ ->
        // 'Select' contains the tags that describe whether this is a defence station, wharf or shipyard.
        let stationClone =
            match station.Station.Select, station.Type with
            | (None, Some "tradingstation") ->
                // These seem to be teladi tranding stations. Replace with something more... interesting
                Some (new XElement(xenonWharf))
            | (None, Some "factory") ->
                // MOST examples in the logs without a tag all seem to be scenarios we weren't going to replace. Khaak, xenon, etc.
                // But TERRAN/SEG has a few defence stations without tags. We'll check their construction plan instead.
                match station.Station.Constructionplan with
                | Some "'ter_defence'" -> Some (new XElement(xenonDefence))
                | Some "'ter_defenceplatform'" -> Some (new XElement(xenonDefence))
                | Some "'pio_defence'" -> Some (new XElement(xenonDefence))
                | _              -> None

            | (None, _) ->
                // the other examples in the logs without a tag all seem to be scenarios we weren't going to replace. Khaak, xenon, etc.
                None
            | (Some select, _) ->
                // create the new xelement clone so we can edit it later as part of the replacement station.
                // We're going to replace different types of NPC buildings with different Xenon stations.
                match select.Tags with
                | "[shipyard]" ->
                    Some (new XElement(xenonShipyard))
                | "[wharf]" | "[equipmentdock]" -> 
                    Some (new XElement(xenonWharf))
                | "[defence]" | "[tradestation]" | "[piratebase]" ->
                    Some (new XElement(xenonDefence)) // For now, replace HAT piratebase with xenon defense.
                | x ->
                    printfn "UNHANDLED STATION TYPE: %s - DEFAULTING TO XENON DEFENCE" x
                    Some (new XElement(xenonDefence))

        match stationClone with
        | None ->
            printfn "  IGNORING DEFAULT [%s]" station.Owner // These will still exist, and probably get wiped pretty quick, unless they're well hidden.
            (None, None)

        | Some stationClone ->
            let id = station.Id
            // create XML tag that will remove the old station
            let remove = new XElement( "remove",
                new XAttribute("sel", $"//god/stations/station[@id='{id}']") // XML remove tag for the station we're replacing with Xenon.
            )
            
            // create a new Xenon station to replace it
            let replacement = new X4GodMod.Station(stationClone)
            replacement.XElement.SetAttributeValue(XName.Get("id"), replacement.Id + "_x_" + id)   // Give it a new unique ID
            // update location. As they're different types (as far as the type provider is concerned), we have to manually set
            // the important zone and macro fields.
            let locationClass= Option.defaultValue "none" station.Location.Class
            let locationMacro= Option.defaultValue "none" station.Location.Macro
            replacement.Location.XElement.SetAttributeValue(XName.Get("class"), locationClass)
            replacement.Location.XElement.SetAttributeValue(XName.Get("macro"), locationMacro)
            logAddStation "REPLACE" replacement

            (Some replacement.XElement, Some remove)  // return an add/remove options,


// Construct an XML element representing a 'replace' tag that will replace a specific quota for a given product.
// example replace line:
// <replace sel="/god/products/product[@id='arg_graphene']/quotas/quota/@galaxy">18</replace>
let product_replace_xml (id:string) (quota_type:string) (quota:int) =
    let xml = new XElement("replace",
        new XAttribute("sel", $"//god/products/product[@id='{id}']/quotas/quota/@{quota_type}"),
        quota
    )
    printfn "  REPLACING PRODUCT %s with quota %s:%i using:\n %s" id quota_type quota (xml.ToString())
    xml

// "products" define the number of production modules that will be created for a faction, scattered
// between their factories. We're going tp increase it for Xenon, and reduce it for other major factions.
let processProduct (product:X4WorldStart.Product) =
    logProduct product
    match product.Owner, product.Ware with
    | "xenon", _ -> Some (product_replace_xml product.Id "galaxy" (product.Quota.Galaxy * 4) )// Xenon get a 4x quota
    | "khaak", _ | "yaki", _ | "scaleplate", _ | "buccaneers", _| "player",_ -> None // These are all fine as is.
    | "terran", "energycells" ->
        // The sectors we've assigned Terra have low solar output, so they're already crippled.
        // We won't reduce production, but we will increase the limit per sector so that they
        // can spawn all their factories in the slightly less bad .4 sunlight sector.
        Some (product_replace_xml product.Id "sector" ( Option.defaultValue 32 product.Quota.Sector * 2) )
    | _ -> Some (product_replace_xml product.Id "galaxy" (product.Quota.Galaxy / 2) ) // Everyone else gets half the quota


// Ok, now to generate the new defense stations that we need around each unsage gate.
// X4Gates.getRequiredDefenseStationLocations will do the work of finding the gates and
// calculating the locations for us, and returning a list that tells us which gate, faction
// and locatio to place the station.
// We need to use that information to find an existing defense station for the faction, and
// then copy it, placing it in to the same zone as the gate (which we have stored in the gate object)
// and then updating the location to the new coordinates, and finally renaming the station to be
// '(gate.name).defense_[id]' so that it's unique.
let generateGateDefenseStations() =
    let gateStations = X4.Gates.getRequiredDefenseStationLocations 3 8000 // 3 stations per gate, 8000m from the gate. Give them almost overlapping fields of fire for long range plasma

    [ for gate, n, location  in gateStations do
        printfn "GENERATING DEFENSE STATION FOR %s GATE %s" gate.Faction gate.ConnectionName
        // find the first defence station for the faction. We want to fail if we find nothing, as that would break the mod.
        let station =
            match find_station gate.Faction "defence" allStations with
            | Some station -> station
            | None -> failwithf "No defense station found for faction %s" gate.Faction
        printfn "  FOUND DEFENSE STATION %s owner:%s" station.Id station.Owner
        let stationClone = new XElement(station.XElement)
        let id = station.Id
        let defenseStation = new X4GodMod.Station(stationClone)
        defenseStation.XElement.SetAttributeValue(XName.Get("id"), gate.ConnectionName + "_bastion_" + n.ToString())   // Give it a new unique ID
        // update location. We want to change this to refer to a zone (the same zone as the gate),
        // and override the specific position. Start by pulling the location entry for the gate,
        // then updating the position to the new coordinates.
        let zone = gate.X4Zone
        // override the location to the gates zone, and set 'matchextension' to false (without it, game ignores mods touching things outside their scope)
        defenseStation.Location.XElement.SetAttributeValue(XName.Get("class"), zone.Class)
        defenseStation.Location.XElement.SetAttributeValue(XName.Get("macro"), zone.Name)
        defenseStation.Location.XElement.SetAttributeValue(XName.Get("matchextension"), "false")
        defenseStation.Location.XElement.SetAttributeValue("solitary", null)    // VIG faction has this attribute set that may cause station placement to fail

        // Now update the precise position.
        let position =
            match defenseStation.Position with
            | Some position -> position.XElement
            | None ->
                printfn "   No position found for station %s" defenseStation.Id
                let posXml = new XElement("position")
                defenseStation.XElement.Add(posXml)
                defenseStation.XElement.Add( new XText("\n")) // Add a newline after each element so the output is readible
                posXml

        position.SetAttributeValue(XName.Get("x"), location.X)
        position.SetAttributeValue(XName.Get("y"), location.Y)
        position.SetAttributeValue(XName.Get("z"), location.Z)

        logAddStation "ADDING" defenseStation
        defenseStation.XElement  // return an add/remove options,
    ]


// Process the GOD file from the core game, and the DLCs. 
// extract the stations and products, tweak some values, then write out a new GOD file.
let generate_god_file (filename:string) =
    // Extract the Xenon stations from the GodModTemplate. We'll use these as templates when we add new xenon stations
    let X4ObjectTemplatesData = X4ObjectTemplates.Load(X4ObjectTemplatesFile)
    let xenonShipyard = (Array.find (fun (elem:X4ObjectTemplates.Station) -> elem.Id = "shipyard_xenon_cluster") X4ObjectTemplatesData.Stations).XElement
    let xenonWharf    = (Array.find (fun (elem:X4ObjectTemplates.Station) -> elem.Id = "wharf_xenon_cluster") X4ObjectTemplatesData.Stations).XElement
    let xenonDefence  = (Array.find (fun (elem:X4ObjectTemplates.Station) -> elem.Id = "xen_defence_cluster") X4ObjectTemplatesData.Stations).XElement


    let (addStations, removeStations)  = 
        [| for station in allStations do yield processStation station X4.Data.allSectors xenonShipyard xenonWharf xenonDefence |] |> splitTuples

    let replaceProducts = [| 
        for product in allProducts do
            match processProduct product with Some product -> yield product | _ -> ()
    |]

    let newDefenseStations = generateGateDefenseStations()

    // Now that everything has been processed, and we've got new stations and products, 
    // we generate the modded XML, and write it out.

    // This is our template output file structure, with the broad sections already created.
    // We have set up the 'add' 'replace' sections with appropriate selectors. We can then
    // extract each Add section by the selector element value, and populate it's underlying
    // XElement in place. eg, search for "//god/stations" to find the 'add' XElement for
    // stations.
    let outGodFile = X4GodMod.Parse("<?xml version=\"1.0\" encoding=\"utf-8\"?>
        <diff>
            <add sel=\"//god/stations\">
            </add>
        </diff>
    "
    )

    let stationsAddElem = find_add_selector "//god/stations" outGodFile.Adds
    // The stations we're replacing with Xenon.
    [ for element in addStations do
        stationsAddElem.XElement.Add(element)
        stationsAddElem.XElement.Add( new XText("\n")) // Add a newline after each element so the output is readible
    ] |> ignore

    // The new defense stations we're adding near gates.
    [ for element in newDefenseStations do
        stationsAddElem.XElement.Add(element)
        stationsAddElem.XElement.Add( new XText("\n")) // Add a newline after each element so the output is readible
    ] |> ignore

    // Add out 'remove' tags to the end of the diff block.
    let diff = outGodFile.XElement // the root element is actually the 'diff' tag.
    let changes = Array.concat [removeStations; replaceProducts ] 
    [ for element in changes do
        diff.Add(element)
        diff.Add( new XText("\n")) // Add a newline after each element so the output is readible
    ] |> ignore

    write_xml_file filename outGodFile.XElement
