// For more information see https://aka.ms/fsharp-console-apps
//open System.Linq
open System.Xml.Linq
//open System.Xml.XPath
open FSharp.Data
open X4MLParser.Data


[<Literal>]
let X4UnpackedDataFolder = __SOURCE_DIRECTORY__ + "/X4_unpacked_data"
[<Literal>]
let X4GodFileCore = X4UnpackedDataFolder + "/core/libraries/god.xml" // Core game data.
let X4GodFileSplit = X4UnpackedDataFolder + "/split/libraries/god.xml" // Core game data.
let X4GodFileTerran = X4UnpackedDataFolder + "/terran/libraries/god.xml" // Core game data.
let X4GodFilePirate = X4UnpackedDataFolder + "/pirate/libraries/god.xml" // Core game data.
let X4GodFileBoron = X4UnpackedDataFolder + "/boron/libraries/god.xml" // Core game data.

[<Literal>]
let X4SectorFileCore = X4UnpackedDataFolder  + "/core/maps/xu_ep2_universe/sectors.xml" // This core sectors file needs to be a literal, as it's also our type provider
let X4SectorFileSplit = X4UnpackedDataFolder + "/split/maps/xu_ep2_universe/dlc4_sectors.xml"   // This one is normal string, as we can load and parse using X4SectorCore literal
let X4SectorFileTerran = X4UnpackedDataFolder + "/terran/maps/xu_ep2_universe/dlc_terran_sectors.xml" 
let X4SectorFilePirate = X4UnpackedDataFolder + "/pirate/maps/xu_ep2_universe/dlc_pirate_sectors.xml" 
let X4SectorFileBoron = X4UnpackedDataFolder + "/boron/maps/xu_ep2_universe/dlc_boron_sectors.xml"   

// The default GOD.xml file, etc, don't have an 'add/replace' XML section, so they can't
// be used as type providers for our output XML. So we've created a template that has
// versions of our types that we need (like station, product) as well as the XML selectors
// for replace/add
[<Literal>]
let X4GodModFile = __SOURCE_DIRECTORY__ + "/mod_templates/god.xml"
[<Literal>]
let X4ObjectTemplatesFile = __SOURCE_DIRECTORY__ + "/mod_templates/object_templates.xml"


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

// TODO: Should we 'unify' all the types using 'Global=true,' parameter?
// This means, for example, every instance of 'Location' type is trested as the same type, no matter
// where it appears in the sample data file. It results in a lot of fields being set to an 'option'
// type, as some will appear in some cases of location, but not in others. So requires some tweaks
// to the parsing code.
// https://fsprojects.github.io/FSharp.Data/library/XmlProvider.html#Global-inference-mode
type X4WorldStart = XmlProvider<X4GodFileCore>
type X4GodMod = XmlProvider<X4GodModFile >
type X4ObjectTemplates = XmlProvider<X4ObjectTemplatesFile>
type X4Sector = XmlProvider<X4SectorFileCore>

type StationMod =
    | Add of XElement
    | Remove of XElement
    | Replace of XElement


// the 'log' functions just extract a bit of data about a station, and log it
// to the terminal for debugging and tracking purposes.
let logStation (station:X4WorldStart.Station) =
    let tags         = match station.Station.Select with | Some tag -> tag.Tags | _ -> "[none]"
    printfn "PROCESSING STATION %s race: %A, owner: %A, type: %A, location: %A:%A, id: %A, station: %A   " tags station.Race station.Owner station.Type station.Location.Class station.Location.Macro station.Id station.Station.Macro

let logAddStation (station:X4GodMod.Station) =
    let tags         = match station.Station.Select with | Some tag -> tag.Tags | _ -> "[none]"
    printfn "   REPLACE STATION %s race: %s, owner: %s, type: %s, location: %s:%s, id: %s, station: \"none\"   " tags station.Race station.Owner station.Type station.Location.Class station.Location.Macro station.Id

let logProduct (product:X4WorldStart.Product) =
    printfn "PROCESSING PRODUCT [%s:%s] %s/%s with quotas %i/%i" product.Owner product.Location.Faction product.Type product.Ware product.Quota.Galaxy (product.Quota.Sector |> Option.defaultValue -1)


// Using the data in sector.xml, which is represented by the X4Sector type, find the name of
// the sector given the name of the zone. the zone is stored as a connection in the sector definition.
let find_sector_from_zone (zone:string) (sectors:X4Sector.Macro list) =
    // Loops through the macros. Each macro will contain a sector. In that sector we'll find connections.
    // Each connection will have zero or more zones for use to check.
    let rec loop (sectors:X4Sector.Macro list) =
        match sectors with
        | [] -> None
        | sector :: rest ->
            //printfn "Checking sector: %s" sector.Name
            let connections = sector.Connections
            let foundConnection = Array.tryFind ( fun (connection:X4Sector.Connection) ->
                                        //printfn "  Checking connection: %s:%s:%s" connection.Ref connection.Macro.Connection connection.Macro.Ref 
                                        connection.Ref = "zones" 
                                        && connection.Macro.Connection = "sector" 
                                        && connection.Macro.Ref =? zone)    // Case insensitive comparison of zone, as the files mix the case of the zone names.
                                    connections
            match foundConnection with
            | Some connection -> Some (sector.Name.ToLower()) // return the sector name, but in lower case, as the case varies in the files. I prefer to make it consistent
            | None -> loop rest
    loop sectors


// Find and return the first occurrence of a station with the given faction and type.
// Used to find things like faction defence stations, wharfs, etc, so that we can move
// the first instance to the factions 'safe' location, while removing the rest.
let find_station (faction:string) (stationType:string) (stations:X4WorldStart.Station[]) =
    Array.find (fun (station:X4WorldStart.Station) -> station.Owner = faction && station.Type = Some stationType) stations

// Given a filename with full path, create all the parent directories recursively if they don't exist.
let check_and_create_dir (filename:string) =
    let dir = System.IO.Path.GetDirectoryName(filename)
    if not (System.IO.Directory.Exists(dir)) then
        System.IO.Directory.CreateDirectory(dir) |> ignore   // Should really catch the failure here. TODO

// Write our XML output to a directory called 'mod'. If the directrory doesn't exist, create it.
let write_xml_file (filename:string) (xml:XElement) =
    let modDir = __SOURCE_DIRECTORY__ + "/mod/after_the_fall"
    let fullname = modDir + "/" + filename
    check_and_create_dir fullname   // filename may contain parent folder, so we use fullname when checking/creating dirs.
    xml.Save(fullname)

// Extract the stations from the 'add' section of a DLCs god diff/mod file.
// While there are many 'add' sections, we're only interested in the one that
// that has the selectior '//god/stations'
let getStationsFromDiff (diff:X4GodMod.Add[]) = 
    let stationsAdd = Array.filter (fun (add:X4GodMod.Add) -> add.Sel = "/god/stations") diff
    [| for stations in stationsAdd do
            for station in stations.Stations do
                yield new X4WorldStart.Station(station.XElement)
    |]



let X4ObjectTemplatesData = X4ObjectTemplates.Load(X4ObjectTemplatesFile)
let X4GodCore = X4WorldStart.Load(X4GodFileCore)
let X4GodSplit = X4GodMod.Load(X4GodFileSplit)
let X4GodTerran = X4GodMod.Load(X4GodFileTerran)
let X4GodPirate = X4GodMod.Load(X4GodFilePirate)
let X4GodBoron = X4GodMod.Load(X4GodFileBoron)

// The DLC stations are of a different type: they're an XML DIFF file, not the GOD
// file type. So we need to pull out the stations from the diff and convert them
// to the same type as the core stations using the underlying XElement.
let splitStations = getStationsFromDiff X4GodSplit.Adds
let terranStations = getStationsFromDiff X4GodTerran.Adds
let pirateStations = getStationsFromDiff X4GodPirate.Adds
let boronStations = getStationsFromDiff X4GodBoron.Adds

// Finally build up an uberlist of all our stations across all DLC and core game.
let allStations = X4GodCore.Stations.Stations 
                |> Array.append splitStations
                |> Array.append terranStations
                |> Array.append pirateStations
                |> Array.append boronStations
                |> Array.toList


//let allProducts = X4GodCore.Products 
//                |> Array.append X4GodSplit.Products 
//                |> Array.append X4GodTerran.Products 
//                |> Array.append X4GodPirate.Products 
//                |> Array.append X4GodBoron.Products 
//                |> Array.toList

// Load the sector data from each individual sector file. We'll combine them in to one list.
let X4SectorCore = X4Sector.Load(X4SectorFileCore)
let X4SectorSplit = X4Sector.Load(X4SectorFileSplit)
let X4SectorTerran = X4Sector.Load(X4SectorFileTerran)
let X4SectorPirate = X4Sector.Load(X4SectorFilePirate)
let X4SectorBoron = X4Sector.Load(X4SectorFileBoron)
let allSectors = X4SectorCore.Macros 
                |> Array.append X4SectorSplit.Macros 
                |> Array.append X4SectorTerran.Macros 
                |> Array.append X4SectorPirate.Macros 
                |> Array.append X4SectorBoron.Macros 
                |> Array.toList

// Extract the Xenon stations from the GodModTemplate. We'll use these as templates when we add new xenon stations
let XenonShipyard = Array.find (fun (elem:X4ObjectTemplates.Station) -> elem.Id = "shipyard_xenon_cluster") X4ObjectTemplatesData.Stations
let XenonWharf    = Array.find (fun (elem:X4ObjectTemplates.Station) -> elem.Id = "wharf_xenon_cluster") X4ObjectTemplatesData.Stations
let XenonDefence  = Array.find (fun (elem:X4ObjectTemplates.Station) -> elem.Id = "xen_defence_cluster") X4ObjectTemplatesData.Stations




// MAIN PROCESSING FUNCTIONS

// Given a station, process it according to our rules. We may replace it
// with a Xenon one, remove it, etc. This function is call once per station
let processStation (station:X4WorldStart.Station) =
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
        | Some "zone" -> find_sector_from_zone (station.Location.Macro |> Option.defaultValue "") allSectors |> Option.defaultValue "none"
        | Some "sector" -> station.Location.Macro |> Option.defaultValue "none"
        | _ -> "none"
    let inTerritory = (X4MLParser.Data.isFactionInSector station.Owner sectorName) || (ignoreFaction station.Owner)

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
                Some (new XElement(XenonWharf.XElement))
            | (None, Some "factory") ->
                // MOST examples in the logs without a tag all seem to be scenarios we weren't going to replace. Khaak, xenon, etc.
                // But TERRAN/SEG has a few defence stations without tags. We'll check their construction plan instead.
                match station.Station.Constructionplan with
                | Some "'ter_defence'" -> Some (new XElement(XenonDefence.XElement))
                | Some "'ter_defenceplatform'" -> Some (new XElement(XenonDefence.XElement))
                | Some "'pio_defence'" -> Some (new XElement(XenonDefence.XElement))
                | _              -> None

            | (None, _) ->
                // the other examples in the logs without a tag all seem to be scenarios we weren't going to replace. Khaak, xenon, etc.
                None
            | (Some select, _) ->
                // create the new xelement clone so we can edit it later as part of the replacement station.
                // We're going to replace different types of NPC buildings with different Xenon stations.
                match select.Tags with
                | "[shipyard]" ->
                    Some (new XElement(XenonShipyard.XElement))
                | "[wharf]" | "[equipmentdock]" -> 
                    Some (new XElement(XenonWharf.XElement))
                | "[defence]" | "[tradestation]" | "[piratebase]" ->
                    Some (new XElement(XenonDefence.XElement)) // For now, replace HAT piratebase with xenon defense.
                | x ->
                    printfn "UNHANDLED STATION TYPE: %s - DEFAULTING TO XENON DEFENCE" x
                    Some (new XElement(XenonDefence.XElement))

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
            let locationClass= match station.Location.Class with | Some x -> x | None -> "none"
            let locationMacro= match station.Location.Macro with | Some x -> x | None -> "none"
            replacement.Location.XElement.SetAttributeValue(XName.Get("class"), locationClass)
            replacement.Location.XElement.SetAttributeValue(XName.Get("macro"), locationMacro)
            logAddStation replacement

            (Some replacement, Some remove)  // return an add/remove options,


let processProduct (product:X4WorldStart.Product) =
    // TODO: Update the product quantities/settings before returning.
    logProduct product
    (Some product, None)


// Given a list of 2 element tuples, split them in to two lists.
// The first list contains all the first elements, the second list contains all the second elements.
// It will strip out any 'None' values.
let splitTuples (tuples:('a option * 'b option)[]) =
    let firsts = [| for (a,_) in tuples do match a with Some x -> yield x | _ -> () |]
    let seconds = [| for (_,b) in tuples do match b with Some x -> yield x | _ -> () |]
    (firsts, seconds)

let godModStations = 
    [| for station in allStations do yield processStation station |] |> splitTuples


let godModProducts = 
    [| for product in X4GodCore.Products do
            let (add, remove) = processProduct product
            match add    with Some product -> yield product | _ -> ()
            match remove with Some product -> yield product | _ -> ()
    |]


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
        <add sel=\"//god/products\">
        </add>
    </diff>
"
)

// Given a selector ID, search an instance of a GodMod xml file for the 'ADD' section
// with that 'sel' value. This will be the XElement we will manipulate.
let find_add_selector sel xml =
   Array.find (fun (elem:X4GodMod.Add) -> elem.Sel = sel) xml


// convert our list of stations in to a list of XElements, so we can add it to our
// output document. The XMK type provider doesn't provide manipulation functions, so we
// can only manipulate the raw xelements.
let (addStations, removeStations) = godModStations 
let outputStations = [|
    for station in addStations do yield station.XElement
|]

let outputProducts = [|
    for product in godModProducts do yield product.XElement
|]

let stationsAdd = find_add_selector "//god/stations" outGodFile.Adds
stationsAdd.XElement.Add(outputStations)
let productsAdd = find_add_selector "//god/products" outGodFile.Adds
productsAdd.XElement.Add(outputProducts)

// Add out 'remove' tags to the end of the diff block.
let diff = outGodFile.XElement // the root element is actually the 'diff' tag.
diff.Add(removeStations)    // Add will append to the end of the diff children,
write_xml_file "libraries/god.xml" outGodFile.XElement

// Copy the content.xml file that describes the mod to the output directory.
System.IO.File.Copy(__SOURCE_DIRECTORY__ + "/mod_templates/content.xml", __SOURCE_DIRECTORY__ + "/mod/after_the_fall/content.xml", true) |> ignore

// let dump = outGodFile.XElement.ToString()
// let sectorName = find_sector_from_zone "zone003_cluster_606_sector002_macro" allSectors
// printfn "Found sector: %A" sectorName

