/// <summary>
/// Functions to handle processing and manipulation of ship data, and generation of ship related mod content.
/// Builds on data loaded in X4.Data to convert to our internal 'shipInfo' and 'shipequipment' types.
/// </summary>

module X4.Ships

open System
open System.IO
open System.Xml
open System.Xml.Linq

open X4.Data
open X4.Utilities

let rand = new Random(12345)    // Seed the random number generator so we get the same results each time, as long as we're not adding new regions or changing territory order.

// Define a few types for ship location that we'll use when we place an abandoned ship
type Position = int * int * int
type Rotation = int * int * int
type ShipLocation = string * string * Position * Rotation   // Ship name, sector name, position, rotation. Should probably use a record type here.

type ShipEquipmentSlot = {
    Name: String
    Class: String
    Size: String
    Group: Option<String>
    Tags: String Set
}

type ShipInfo = {
    Name: String
    MacroName: String
    Size: String
    DLC: String
    Type: String
    Thruster: String
    ComponentRef: String
    ComponentFile: String
    Macro: X4ShipsMacro.Macro
    Connections: X4Ships.Connection array
    EquipmentSlots: ShipEquipmentSlot list
}

// Extracts the groups from a list of ship equipment slots.
let shipEquipmentGroups (allSlots: ShipEquipmentSlot list) =
    allSlots
    |> List.choose (fun slot -> slot.Group |> Option.map (fun group -> ( (group, slot.Class), slot)))  // Filter out anything without a group
    |> List.groupBy fst     // New list grouped by group and class of item.
    |> List.map (fun (groupBy, slots) -> (groupBy, List.map snd slots))    // At this point our 'slots' is actually (group,slots) list, due to our previous processing. Reduce down to slots again
    |> List.sortBy fst

// Some tags are not relevant for selection/slot match
let tagsToIgnore = set [ 
    "component"; "symmetry"; 
    "symmetry_1"; "symmetry_2"; "symmetry_right"; "symmetry_left"; 
    "platformcollision"; "mandatory"; "notupgradeable"
    // the following are important after all
    // "hittable"; "unhittable"; 
    ]

let componentSizeClasses = set ["small"; "medium"; "large"; "extralarge"] // I really wish there was some kind of consistency when it comes to referring to sizes.
let shipEquipmentClasses = [
    // Allow us to filter down all assets to the ship equipment we're interested in.
    // Ship mounted equipment
    "engine"; "shieldgenerator"; "weapon"; "missileturret"; "missilelauncher"; "turret"; 
    // Ship deployables.
    "missile"; "resource_probe"; "satellite"
]

let shipEquipmentConnectionTags = set ["weapon"; "turret"; "shield"; "engine"; "thruster" ]


// Checks to see if the ship connection is an equipment slot connection,
// and if so, parses it to return the relevant information about the equipment slot.
let parseConnectionForEquipmentSlot (connection:X4Ships.Connection) =
    // We determine whether the connection is an equipment slot by checking the tags.
    // If the tags contains one of the special equipment slot tags defined in
    // ShipEquipmentConnectionTag, then it's an equipment slot.

    // split the tag string in to a set of tags, and trim excess whitespace
    let tags = Set.difference (tagStringToSet connection.Tags) tagsToIgnore

    // find the first tag that matches one of the equipment slot tags. ie, the element, if any,
    // that appears both in the tags set and in the ShipEquipmentConnectionTag set.
    let equipmentTag = Set.intersect tags shipEquipmentConnectionTags |> Seq.tryHead

    match equipmentTag with
    | None -> None // Not an equipment slot, so return None
    | Some tag ->
        Some {
            Name = connection.Name.Trim();
            Class = tag;
            Size = Set.intersect tags componentSizeClasses |> Seq.tryHead |> Option.defaultValue "unknown"
            Group = connection.Group |> Option.map (fun group -> group.Trim());
            Tags = tags;
        }


let LoadShipComponents (entry:Index) (macro:X4ShipsMacro.Macros) =
    let componentEntry = Array.Find (AllComponentMacros, (fun componentEntry -> componentEntry.Name =? macro.Macro.Component.Ref))
    let componentFilename = X4UnpackedDataFolder + "/" + componentEntry.File.Replace("\\", "/")

    // Lets load the compoenent file and parse it.
    try
        let parsed = X4Ships.Load(componentFilename)
        let name   = parsed.Component.Name
        let size   = parsed.Component.Class
        let connections = parsed.Component.Connections

        // Not all macro files include the 'type' property, so we need to check if it exists.
        let shiptype =
            try
                macro.Macro.Properties.Ship.Type
            with
            | _ex -> "unknown"
        let thruster =
            try
                macro.Macro.Properties.Thruster.Tags
            with
            | _ex -> "unknown"

        // printfn "Loaded ship: %-35s %-35s from %s" name macro.Macro.Component.Ref componentFilename

        Some {
            Name = name;
            Size = size;
            Type = shiptype;
            Thruster = thruster;
            DLC = entry.DLC;
            MacroName = entry.Name;
            Macro = macro.Macro;
            ComponentRef = macro.Macro.Component.Ref;
            ComponentFile = componentFilename;
            Connections = connections;
            EquipmentSlots = connections |> Array.choose parseConnectionForEquipmentSlot |> Array.toList;
            }
    with
    | ex ->
        printfn $"Error loading ship:  {componentFilename}: {ex.Message}"
        None

// Pull all the information about all the ships in the game by filtering down to the ship macros in the index macros,
// then loading those files; finding the name of relevant ship component reference, then using that reference to find
// the component file from the component index to get the file that contains the actual ship definition we're interested in.
// This will replace the 'allShipMacros' function.
let allShips =
    // 1. Get all the ship macros from the index macros, and filter them to only those that start with "ship_"
    AllIndexMacros
    |> Array.filter (fun entry -> entry.Name.StartsWith "ship_")
    |> Array.filter (fun entry -> not (entry.Name.Contains "_xs_")) // we don't wan't xs ships - plus they have no thruster entry, breaking our template file parser.
    // 2. For each ship macro, find the file it points to, and load it.
    // Ships are actually defined in two files. the macro file, and the 'component' file.
    // The macro file contains the ship definition, and the component file contains the ship component definition
    |> Array.choose (fun entry ->
        try
            // Get the file name from the entry, and load it.
            let fileName = X4UnpackedDataFolder + "/" + entry.File
            if File.Exists fileName then
                Some (entry, X4ShipsMacro.Load fileName)
            else
                printfn "Warning: Ship macro file %s not found." fileName
                None
        with
        | ex ->
            printfn $"Error loading ship macro: {entry.Name}: {ex.Message}"
            None
    )
    // 3. From the loaded macro file, pull out the reference to the ship asset/component file,
    // and look up the ship asset file in the component index.
    |> Array.choose ( fun (entry, macro) -> LoadShipComponents entry macro)
    |> Array.toList


let findShipByName (shipName:string) =
    // Find a ship by its name, case insensitive.
    allShips |> List.tryFind (fun ship -> ship.Name =? shipName)


// Get all the assets, and filter them down to only the classes that are ship
// equipment we need to generation ship loadouts.
// Convert the xmln asset to a simplified ShipEquipment type, which gives us easy access
//  to the name, macro, class, tags, size and component connection.
let allShipEquipment =
    // There are some assets that are not valid for loadouts, even if their tags match.
    let assetsToIgnore = [
        "weapon_gen_lasertower_01_mk2"; "weapon_gen_lasertower_01_mk1"
        "_xen_"; "_kha_"; "generic_"
    ]

    // Find out all the different unique classes of assests
    X4.Data.allAssets
    |> List.filter (fun asset -> shipEquipmentClasses |> List.contains asset.Class)
    |> List.filter (fun asset -> 
        not (assetsToIgnore 
             |> List.exists (fun ignore -> asset.Name.Contains ignore) ))
    |> List.map (fun asset ->
        option {
            // Find the connection in the assets list of connections that has 'compononent' in its tags.
            // let! will early return if the result is None here. ie, the asset has no component connections.
            let! componentConnection =
                asset.Connections
                |> Array.tryFind (fun connection ->
                    tagStringToList connection.Tags
                    |> List.contains "component"
                )

            // Parse the tags string, stripping out  the tags we want to ignore.
            let tags = Set.difference (tagStringToSet componentConnection.Tags) tagsToIgnore
            let size =
                // one of the tags is the size class, so we try find any of the valid size tags in the tag list.
                tags
                |> Set.intersect componentSizeClasses
                |> Seq.tryHead
                |> Option.defaultValue "none"

            return {
                Name  = asset.Name
                MacroName = asset.Name + "_macro"
                Class = asset.Class
                Tags  = tags
                Size  = size
                ComponentName = componentConnection.Name
                ComponentConnection = componentConnection
                Connections = asset.Connections
            }
        }
    )
    |> List.choose id


// Searches though all ship equipment for the items that 'match' the given tags.
// In most cases, the all tags on the equpment MUST also match the slot's tags.
// The exception to this rule is the 'one of' tags, which can be used to permit
// a range of different 'use cases', like 'mining' or 'combat'.
let findMatchingEquipmentForTags (tags:Set<String>) =
    // This subset of tags basically give a 'class' to the components. And some slots can handle
    // more than one class of component. eg, some miners can mount both mining and combat turrets.
    // Some ships can mount both 'missile' or 'combat' weapons, and so on.
    let shipEquipmentOneOfTags = set [ "standard"; "highpower"; "missile"; "mining"; "combat" ]

    // Find the 'oneOfTags' that are present in the slot's tags.
    // let validOneOfTags = tags |> List.filter (fun tag -> List.contains tag shipEquipmentOneOfTags)
    let validOneOfTags = Set.intersect tags shipEquipmentOneOfTags

    // Now filter through all of the ship equipment, finding those that have the oneOfTags,
    // and also match on all the rest of the tags.
    allShipEquipment
    |> List.filter (fun equipment ->
        // so, the match is complicated with a bunch of possilibites.
        // First: the 'validOneOf' tags: If neither slot, nor item have any of these
        // then it's fine.
        // if EITHER of them have any, we need to check for matches.
        let eAny = Set.intersect equipment.Tags validOneOfTags
        let sAny = Set.intersect tags validOneOfTags

        if Set.isEmpty eAny && Set.isEmpty sAny then
            // If neither have any of the 'one of' tags, then just check for exact matches
            equipment.Tags = tags
        elif not (Set.isEmpty eAny) && not (Set.isEmpty sAny) && (eAny.IsSubsetOf sAny) then
            // If the equipment tags has a subset of the slot 'any of' tags 
            // e.g, equipment is 'mining', slot is ['mining'; 'combat'],
            // then we check to see if the remaining tags match.
            let eRemainingTags = Set.difference equipment.Tags shipEquipmentOneOfTags
            let sRemainingTags = Set.difference tags shipEquipmentOneOfTags
            sRemainingTags = eRemainingTags
        else
            // If we reach here, it means one of them has 'one of' tags and the other doesn't.
            //This means that it's not a match.
            false
    )

let findMatchingEquipmentForSlot (slot: ShipEquipmentSlot) =
    findMatchingEquipmentForTags slot.Tags

let dumpAllShipEquipment() =
    printfn "\nAll Equipment:"
    allShipEquipment
    |> List.iter (X4.Data.dumpEquipment "")

let printShipInfo (ship:ShipInfo) =
    let formatShipSlot (slot: ShipEquipmentSlot) =
        sprintf "  %-30s %-10s %-10s %-25s | %s" slot.Name slot.Class slot.Size (Option.defaultValue "" slot.Group) 
            (slot.Tags |> Seq.map (fun tag -> tag.Trim()) |> String.concat " ")

    // Print the ship info in a nice format.
    printfn "\n Ship: %s" ship.Name
    // ship.Connections
    // |> Seq.iter (fun connection -> printfn "  Connection %-50s/%-20s / %s" connection.Name (Option.defaultValue "" connection.Group ) connection.Tags) 
    // printfn " Discovered Equipment Slots:"
    ship.EquipmentSlots
    |> Seq.iter (fun slot ->
        printfn "%s" (formatShipSlot slot)
        // Now find, and print out valid equipment for this slot.
        slot
        |> findMatchingEquipmentForSlot
        |> List.iter (X4.Data.dumpEquipment "    - ")
    )
    printfn "  Equipmment Groups"
    ship.EquipmentSlots
    |> shipEquipmentGroups
    |> Seq.iter (fun ((group, slotClass), slots) ->
        printfn "  Group: %-25s x%d  %s " group slots.Length (formatShipSlot slots[0])
    )

let dumpShips() =
    printfn "All Ship Macros:"
    for ship in allShips do printfn "macro: %s," (ship.MacroName)


// List of all ships that are valid candidates for being generated as abandoned ships.
let abandonedShipsList =
    // Our filters. Factions we'll look for, and tags we'll omit. Both must be true.
    let factions = [ "_arg_"; "_par_"; "_tel_"; "_spl_"; "_ter_"; "_atf_"; "_yak_"; "_pir_"; ]  // Removed boron '_bor_', as they're spawning without engins
    let omit = [
        "_xs_"; "_plot_"; "_landmark_"; "_story"; "_highcapacity_";
        "ship_spl_xl_battleship_01_a_macro"; "ship_pir_xl_battleship_01_a_macro"; // specific ships that seems unsupported or don't exist in game
        // Some terran destroyers that are spawning without main guns.
        "ship_atf_l_destroyer_01_a_macro"; "ship_atf_xl_battleship_01_a_macro"; "ship_ter_l_destroyer_01_a_macro"
    ]

    allShips
    |> List.map (fun ship -> ship.MacroName)
    |> List.filter (
        fun shipName ->
            ( factions |> List.exists (fun tag -> shipName.Contains tag) )        // Must contain one of the factions we're interested in.
            && (not ( omit |> List.exists (fun tag -> shipName.Contains tag) )))  // Must not contain any of the tags we're not interested in from the omit list.


// Quickly filter by a search string substring. eg, 'bor' will return all the Boron ships by filtering for '_bor_'.
let rec filterListBy (searchTags:string list) (ships:string list) =
    match searchTags with
    | [] -> ships
    | H::T ->
        ships
        |> List.filter (fun x -> x.Contains("_" + H.ToLower() + "_"))
        |> filterListBy T

// Filter the abandoned ships list by a list of search tags. eg, ['bor', 's'] will return all Boron small ships.
// ['tel', 'xl', 'carrier'] will return all Teladi extra large carriers.
let filterBy (search:string list) =
    filterListBy search abandonedShipsList

let militaryShips =
    List.concat [
        filterBy ["corvette"]
        filterBy ["gunboat"]
        filterBy ["frigate"]
        filterBy ["destroyer"]
        filterBy ["battleship"]
        filterBy ["carrier"]
        filterBy ["resupplier"]
        filterBy ["fighter"]
        filterBy ["heavyfighter"]
        filterBy ["bomber"]
        filterBy ["scout"]
    ]

let economyShips =
    List.concat [
        filterBy ["miner"]
        filterBy ["builder"]
        filterBy ["trans"]
    ]


// given a sector and a list of possible ships, select one of the ships,
// and assign it coordinates in the given sector.
let generateRandomAbandonedShipFromListInSector (sector:string) (shipList:string list) :ShipLocation =
    let ship = shipList.[rand.Next(shipList.Length)]
    // generate random coordinates within the sector, in KM offset from sector center (different from other coordinates)
    let x, y, z = rand.Next(-160, 160), rand.Next(-10, 10), rand.Next(-180, 180)
    // generate random yaw and pitch
    let yaw, pitch, roll = rand.Next(-180, 180), rand.Next(-180, 180), rand.Next(-180, 180)
    (ship, sector, (x, y, z), (yaw, pitch, roll))

// given a list of possible ships, select one, and place it randomly in any of the
// unsafe sectors in the game.
let generateRandomAbandonedShipFromList (shipList:string list) =
    let sector = X4.Data.selectRandomUnsafeSector() // We don't want these wrecks to be in the faction sectors.
    generateRandomAbandonedShipFromListInSector sector.Name shipList

// Generate COUNT random abandoned military ships of the given size in a random unsafe sector.
// 'size' is one of 'XL', 'L', 'm', 's'. - the standard X4 ship size classes.
let generateRandomMilitaryAbandonedShips (count:int) (size:string) =
    let ships = filterListBy [size] militaryShips
    [ for i in 1..count -> generateRandomAbandonedShipFromList ships ]

// As above, but for economy ships.
let generateRandomEconomyAbandonedShips (count:int) (size:string) =
    let ships = filterListBy [size] economyShips
    [ for i in 1..count -> generateRandomAbandonedShipFromList ships ]

// This function generates a bunch of abandoned ships near each other, as if a major battle occurred.
// The parameters determine how many of each class are in the field. Ships are clustered within 5km of a point
// in a random unsafe sector.
let generateBattlefield (countXL:int) countL countM countS =
    printfn "GENERATING BATTLEFIELD: XL: %i, L: %i, M: %i, S: %i" countXL countL countM countS
    // First generate the ships for each class.
    let xl, l, m, s =
        generateRandomMilitaryAbandonedShips countXL "xl",
        generateRandomMilitaryAbandonedShips countL "l",
        generateRandomMilitaryAbandonedShips countM "m",
        generateRandomMilitaryAbandonedShips countS "s"

    // Then we update the location of each ship to be within 5km of the location of the first ship.
    // First find the location of the first ship. We concat all the size classes, as we don't if any
    // size classes were empty for this battlefield.
    let _ship, sector, (x, y, z), _rotation  = (List.concat [xl;l;m;s]).[0]
    // Now update every ships sector and location to be near the first ship, keeping other data the same.
    List.concat [
        [ for (ship, _sector, _, rotation) in xl -> (ship, sector, (x + rand.Next(-5, 5), y + rand.Next(-5, 5), z + rand.Next(-5, 5)) , rotation) ]
        [ for (ship, _sector, _, rotation) in l  -> (ship, sector, (x + rand.Next(-5, 5), y + rand.Next(-5, 5), z + rand.Next(-5, 5)) , rotation) ]
        [ for (ship, _sector, _, rotation) in m  -> (ship, sector, (x + rand.Next(-5, 5), y + rand.Next(-5, 5), z + rand.Next(-5, 5)) , rotation) ]
        [ for (ship, _sector, _, rotation) in s  -> (ship, sector, (x + rand.Next(-5, 5), y + rand.Next(-5, 5), z + rand.Next(-5, 5)) , rotation) ]
   ]

// Generate the XML diff for placing an abandoned ship in the game based
// on the ship, sector, position and rotation given as parameters.
let ProcessShip ((ship, sector, (x, y, z), (yaw, pitch, roll)):ShipLocation) =
    // Interestingly, the units of KM and deg are specified in the XML attribute fields for abandoned ships.
    // I've not seen this elsewhere, and don't know if it's necessary, but for safety I'll duplicate it.
    printfn "GENERATING ABANDONED SHIP: %s, Sector: %s, Position: %A, Rotation: %A" ship sector (x, y, z) (yaw, pitch, roll)
    let xml = $"""
    <add sel="/mdscript[@name='PlacedObjects']/cues/cue[@name='Place_Claimable_Ships']/actions">
        <find_sector name="$sector" macro="macro.{sector}"/>
        <do_if value="$sector.exists">
          <create_ship name="$ship" macro="macro.{ship}" sector="$sector">
            <owner exact="faction.ownerless"/>
            <position x="{x}km" y="{y}km" z="{z}km"/>
            <rotation yaw="{yaw}deg" pitch="{pitch}deg" roll="{roll}deg"/>
          </create_ship>
        </do_if>
    </add>
    """
    // Using the textreader instead of XElement.Parse preserves whitespace and carriage returns in our output.
    let xtr = new XmlTextReader(new System.IO.StringReader(xml));
    XElement.Load(xtr);

// Create a list of random ships, assign them to random sectors, then generate XML that will place
// them as abandoned ships in the game.
// We don't want it completely random, as we want to make sure there's a good mix of ships in the game.
// We lean slighly towards generated economy ships vs military, though there's plenty of both.
// there should be, on average, one or two ships per sector.
let generate_abandoned_ships_file (filename:string) =
    let shipDiff =  List.concat [
        // A bunch of ships in unsafe space to being
        generateRandomMilitaryAbandonedShips 4 "xl" |> List.map ProcessShip
        generateRandomMilitaryAbandonedShips 6 "l"  |> List.map ProcessShip
        generateRandomMilitaryAbandonedShips 6 "m"  |> List.map ProcessShip
        generateRandomMilitaryAbandonedShips 6 "s"  |> List.map ProcessShip
        generateRandomEconomyAbandonedShips 3  "xl" |> List.map ProcessShip
        generateRandomEconomyAbandonedShips 12 "l"  |> List.map ProcessShip
        generateRandomEconomyAbandonedShips 8 "m"   |> List.map ProcessShip
        generateRandomEconomyAbandonedShips 6 "s"   |> List.map ProcessShip
        [filterBy ["spl"; "xl"; "carrier"]    |> generateRandomAbandonedShipFromList |> ProcessShip]      // Make sure there's at least one Raptor!
        // Until we figure out how to generate these with faction specific equipment, we'll leave them out. Currently, they're spawning without main batteries.
        //[filterBy ["atf"; "xl"; "battleship"] |> generateRandomAbandonedShipFromList |> ProcessShip]   // And Asgard!
        //[filterBy ["atf"; "l"; "destroyer"]   |> generateRandomAbandonedShipFromList |> ProcessShip]     // And Syn.

        // Lets generate a few battlefields of varying sizes
        generateBattlefield 1 3 2 2 |> List.map ProcessShip
        generateBattlefield 0 3 4 0 |> List.map ProcessShip
        generateBattlefield 0 1 4 5 |> List.map ProcessShip
        generateBattlefield 0 0 4 8 |> List.map ProcessShip
        generateBattlefield 0 0 8 2 |> List.map ProcessShip


        // followed by a bunch of M & S in safe space.
        [
            for i in 1..6 ->
                militaryShips |> filterListBy ["m"] |> (generateRandomAbandonedShipFromListInSector (X4.Data.selectRandomSafeSector().Name)) |> ProcessShip
            for i in 1..8 ->
                economyShips  |> filterListBy ["m"] |> (generateRandomAbandonedShipFromListInSector (X4.Data.selectRandomSafeSector().Name)) |> ProcessShip
            for i in 1..8 ->
                militaryShips |> filterListBy ["s"] |> (generateRandomAbandonedShipFromListInSector (X4.Data.selectRandomSafeSector().Name)) |> ProcessShip
            for i in 1..10 ->
                economyShips  |> filterListBy ["s"] |> (generateRandomAbandonedShipFromListInSector (X4.Data.selectRandomSafeSector().Name)) |> ProcessShip

            // ok, a couple large l economy ship.
            for i in 1..2 ->
                economyShips  |> filterListBy ["l"] |> (generateRandomAbandonedShipFromListInSector (X4.Data.selectRandomSafeSector().Name)) |> ProcessShip

        ]

    ]

    // Create the new XML Diff document to contain our region additions
    let diff = XElement.Parse(
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>
        <diff>
        </diff>
        ")

    // Now add the region changes, one by one, to the the xml diff.
    [| for element in shipDiff do
        diff.Add(element)
        diff.Add( new XText("\n")) // Add a newline after each element so the output is readible
    |] |> ignore

    WriteModfiles.write_xml_file "core" filename diff
