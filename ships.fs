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
open X4.Types
open X4.Utilities

// All the abandoned-ship balance values (counts, battlefields, scatter ranges) live in tuning.fs.
module Tune = X4.Tuning.AbandonedShips

let rand = new Random(12345) // Seed the random number generator so we get the same results each time, as long as we're not adding new regions or changing territory order.

// Define a few types for ship location that we'll use when we place an abandoned ship
type Position = int * int * int
type Rotation = int * int * int
type ShipLocation = string * string * Position * Rotation // Ship name, sector name, position, rotation. Should probably use a record type here.


// Extracts the groups from a list of ship equipment slots.
let shipEquipmentGroups (allSlots: ShipEquipmentSlot list) =
    allSlots
    |> List.choose (fun slot -> slot.Group |> Option.map (fun group -> ((group, slot.Class), slot))) // Filter out anything without a group
    |> List.groupBy fst // New list grouped by group and class of item.
    |> List.map (fun (groupBy, slots) -> (groupBy, List.map snd slots)) // At this point our 'slots' is actually (group,slots) list, due to our previous processing. Reduce down to slots again
    |> List.filter (fun ((_, className), _) -> className <> "engine") // While engines are listed with a group for L ships, they should always be specified in the macros section.
    |> List.sortBy fst



let findShipByName (shipName: string) =
    // Find a ship by its name, case insensitive.
    allShips |> List.tryFind (fun ship -> ship.Name =? shipName)

let findShipByMacroName (macroName: string) =
    // Find a ship by its macro name, case insensitive.
    allShips |> List.tryFind (fun ship -> ship.MacroName =? macroName)



// Searches though all ship equipment for the items that 'match' the given tags.
// In most cases, the all tags on the equpment MUST also match the slot's tags.
// The exception to this rule is the 'one of' tags, which can be used to permit
// a range of different 'use cases', like 'mining' or 'combat'.
let findMatchingEquipmentForTagsImpl (slotTags: Set<String>) =
    // This subset of tags basically give a 'class' to the components. And some slots can handle
    // more than one class of component. eg, some miners can mount both mining and combat turrets.
    // Some ships can mount both 'missile' or 'combat' weapons, and so on.
    // If the equipment item has one of these tags, then the *slot* must have them too.
    let OneOfTags =
        set [ "standard"; "highpower"; "missile"; "mining"; "combat"; "advanced" ] // Advanced is used by Boron and Terran


    // Now filter through all of the ship equipment, finding those that have the oneOfTags,
    // and also match on all the rest of the tags.
    // This logic really needs to be cleaned up further, though at least it now works.
    allShipEquipment
    |> List.filter (fun equipment ->
        // First check: does the equipment have any 'special' tags that match the valid ones for this slot?
        let equipmentOneOfTags = Set.intersect equipment.Tags OneOfTags
        // Find the 'oneOfTags' that are present in the slot's tags.
        let slotOneOfTags = Set.intersect slotTags OneOfTags
        // And the equipment tags that match any 'one of' tags in the slot.
        let eAny = Set.intersect equipment.Tags slotOneOfTags

        if Set.isEmpty equipmentOneOfTags && Set.isEmpty slotOneOfTags then
            // If neither have any of the 'one of' tags, then just check for exact matches
            equipment.Tags = slotTags
        elif not (equipmentOneOfTags.IsSubsetOf slotOneOfTags) then
            // If the equipment item has one of these tags, then the *slot* must have them too.
            // e.g. If slot allows ['standard', 'combat'], and equipment has ['standard', 'mining'], it should fail because 'mining' is not allowed.
            false
        elif not (Set.isEmpty eAny) then
            // If the equipment tags has a subset of the slot 'any of' tags
            // e.g, equipment is 'mining', slot is ['mining'; 'combat'],
            // then we check to see if the remaining tags match.
            let eRemainingTags = Set.difference equipment.Tags OneOfTags
            let sRemainingTags = Set.difference slotTags OneOfTags
            sRemainingTags = eRemainingTags
        else
            // If we reach here, it means one of them has 'one of' tags and the other doesn't.
            //This means that it's not a match.
            false)

// Every tag that appears on at least one piece of equipment. A slot tag outside this
// set can never contribute to a match. As of 9.0 some ship-specific slot tags no longer
// exist on the equipment side (e.g. the Asgard slot still has 'atf_battleship_01' but
// its main gun component lost it), so such tags are dropped as a matching last resort.
let allKnownEquipmentTags =
    allShipEquipment |> List.map (fun e -> e.Tags) |> Set.unionMany

let findMatchingEquipmentForTags (tags: Set<String>) =
    // Terran L turrets are flagged with 'hittable', but there are no L turrets with 'hittable'.
    // So if there are no matching tags, remove 'hittable' from tags and try again.
    // In other cases, 'hittable' is important, as there might be two versions of M turrets, for example.
    // One for M ships, where they are not hittable, and one for L ships.
    match findMatchingEquipmentForTagsImpl tags with
    | [] ->
        let tags =
            if tags.Contains "hittable" then Set.remove "hittable" tags else tags

        match findMatchingEquipmentForTagsImpl tags with
        | [] when not ((Set.intersect tags allKnownEquipmentTags) = tags) ->
            // Retry without the slot tags that exist on no equipment at all.
            findMatchingEquipmentForTagsImpl (Set.intersect tags allKnownEquipmentTags)
        | matches -> matches
    | matches -> matches


let findMatchingEquipmentForSlot (slot: ShipEquipmentSlot) = findMatchingEquipmentForTags slot.Tags

// Find any asset that includes the specified search tags.
// Useful for debugging rather than assigning equipment, as it ignores
// the subtleties relating to matching equipmenmt to slots.
let findMatchingEquipment (searchTags: Set<String>) (equipment: list<EquipmentInfo>) =
    // Find an asset by its name, case insensitive.
    equipment
    // Filter down to only those that match the tags.
    |> List.filter (fun equipment ->
        // Check if any of the connections have the tags we're looking for.
        searchTags.IsSubsetOf equipment.Tags)


// Helper to find matching equipment for a set of tags and pick a random one.
// Returns None (with a warning) when nothing matches. As of 9.0 some hardpoints
// have tag sets that no equipment matches exactly (e.g. dedicated missile launcher
// slots tagged without 'combat', while all launchers still carry it), so callers
// for optional slots like weapons should leave the slot empty rather than abort.
let tryPickEquipment tags : EquipmentInfo option =
    match findMatchingEquipmentForTags tags with
    | [] ->
        printfn "    WARNING: No equipment matches slot tags %A. Leaving slot empty." (Set.toList tags)
        None
    | matches -> Some matches.[rand.Next(matches.Length)]

// As tryPickEquipment, but for slots a ship can't function without (engines, shields,
// thrusters): fail generation loudly instead of leaving them empty.
let pickEquipment tags : EquipmentInfo =
    match findMatchingEquipmentForTags tags with
    | [] -> failwithf "No equipment found matching slot tags: %A" (Set.toList tags)
    | matches -> matches.[rand.Next(matches.Length)]

let dumpEquipmentInfo (prefix: string) (info: EquipmentInfo) =
    let tags =
        info.Tags |> Set.toList |> List.map (fun tag -> tag.Trim()) |> String.concat " "

    printfn "%s%45s %-15s %-10s %-22s | %s" prefix info.Name info.Class info.Size info.ComponentName tags

let dumpAllShipEquipment () =
    printfn "\nAll Equipment:"
    allShipEquipment |> List.iter (dumpEquipmentInfo "")


let printShipInfo (ship: ShipInfo) =
    let formatShipSlot (slot: ShipEquipmentSlot) =
        sprintf
            "  %-30s %-10s %-10s %-25s | %s"
            slot.Name
            slot.Class
            slot.Size
            (Option.defaultValue "---" slot.Group)
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
        slot |> findMatchingEquipmentForSlot |> List.iter (dumpEquipmentInfo "    - "))

    printfn "  Equipmment Groups"

    ship.EquipmentSlots
    |> shipEquipmentGroups
    |> Seq.iter (fun ((group, slotClass), slots) ->
        printfn "  Group: %-25s x%d  %s " group slots.Length (formatShipSlot slots[0]))


let dumpShips () =
    printfn "All Ship Macros:"

    for ship in allShips do
        printfn "macro: %s," (ship.MacroName)




// List of all ships that are valid candidates for being generated as abandoned ships.
let abandonedShipsList =
    // Our filters. Factions we'll look for, and tags we'll omit. Both must be true.
    let factions = [
        "_arg_"
        "_par_"
        "_tel_"
        "_spl_"
        "_ter_"
        "_atf_"
        "_yak_"
        "_pir_"
        "_bor_"
    ]

    let omit = [
        "_xs_"
        "_plot_"
        "_landmark_"
        "_story"
        "_highcapacity_"
        "ship_spl_xl_battleship_01_a_macro"
        "ship_pir_xl_battleship_01_a_macro" // specific ships that seems unsupported or don't exist in game
    // Some terran destroyers that are spawning without main guns. <<should be fixed now with custom loadouts>>
    //"ship_atf_l_destroyer_01_a_macro"
    //"ship_atf_xl_battleship_01_a_macro"
    //"ship_ter_l_destroyer_01_a_macro"
    ]

    allShips
    |> List.map (fun ship -> ship.MacroName)
    |> List.filter (fun shipName ->
        (factions |> List.exists (fun tag -> shipName.Contains tag)) // Must contain one of the factions we're interested in.
        && (not (omit |> List.exists (fun tag -> shipName.Contains tag)))) // Must not contain any of the tags we're not interested in from the omit list.


// Quickly filter by a search string substring. eg, 'bor' will return all the Boron ships by filtering for '_bor_'.
let rec filterListBy (searchTags: string list) (ships: string list) =
    match searchTags with
    | [] -> ships
    | h :: t ->
        ships
        |> List.filter (fun x -> x.Contains("_" + h.ToLower() + "_"))
        |> filterListBy t

// Filter the abandoned ships list by a list of search tags. eg, ['bor', 's'] will return all Boron small ships.
// ['tel', 'xl', 'carrier'] will return all Teladi extra large carriers.
let filterBy (search: string list) = filterListBy search abandonedShipsList

let militaryShips =
    List.concat [
        filterBy [ "corvette" ]
        filterBy [ "gunboat" ]
        filterBy [ "frigate" ]
        filterBy [ "destroyer" ]
        filterBy [ "battleship" ]
        filterBy [ "carrier" ]
        filterBy [ "resupplier" ]
        filterBy [ "fighter" ]
        filterBy [ "heavyfighter" ]
        filterBy [ "bomber" ]
        filterBy [ "scout" ]
    ]

let economyShips =
    List.concat [ filterBy [ "miner" ]; filterBy [ "builder" ]; filterBy [ "trans" ] ]


// given a sector and a list of possible ships, select one of the ships,
// and assign it coordinates in the given sector.
let generateRandomAbandonedShipFromListInSector (sector: string) (shipList: string list) : ShipLocation =
    let ship = shipList.[rand.Next(shipList.Length)]
    // generate random coordinates within the sector, in KM offset from sector center (different from other coordinates)
    let x, y, z =
        rand.Next(-Tune.SectorScatterX, Tune.SectorScatterX),
        rand.Next(-Tune.SectorScatterY, Tune.SectorScatterY),
        rand.Next(-Tune.SectorScatterZ, Tune.SectorScatterZ)
    // generate random yaw and pitch
    let yaw, pitch, roll =
        rand.Next(-180, 180), rand.Next(-180, 180), rand.Next(-180, 180)

    (ship, sector, (x, y, z), (yaw, pitch, roll))

// given a list of possible ships, select one, and place it randomly in any of the
// unsafe sectors in the game.
let generateRandomAbandonedShipFromList (shipList: string list) =
    let sector = selectRandomUnsafeSector () // We don't want these wrecks to be in the faction sectors.
    generateRandomAbandonedShipFromListInSector sector shipList

// Generate COUNT random abandoned military ships of the given size in a random unsafe sector.
// 'size' is one of 'XL', 'L', 'm', 's'. - the standard X4 ship size classes.
let generateRandomMilitaryAbandonedShips (count: int) (size: string) =
    let ships = filterListBy [ size ] militaryShips
    [ for i in 1..count -> generateRandomAbandonedShipFromList ships ]

// As above, but for economy ships.
let generateRandomEconomyAbandonedShips (count: int) (size: string) =
    let ships = filterListBy [ size ] economyShips
    [ for i in 1..count -> generateRandomAbandonedShipFromList ships ]

// This function generates a bunch of abandoned ships near each other, as if a major battle occurred.
// The parameters determine how many of each class are in the field. Ships are clustered within
// Tuning.AbandonedShips.BattlefieldSpread km of a point in a random unsafe sector.
let generateBattlefield (countXL: int) (countL: int) (countM: int) (countS: int) =
    printfn "GENERATING BATTLEFIELD: XL: %i, L: %i, M: %i, S: %i" countXL countL countM countS
    let spread = Tune.BattlefieldSpread
    // First generate the ships for each class.
    let xl, l, m, s =
        generateRandomMilitaryAbandonedShips countXL "xl",
        generateRandomMilitaryAbandonedShips countL "l",
        generateRandomMilitaryAbandonedShips countM "m",
        generateRandomMilitaryAbandonedShips countS "s"

    // Then we update the location of each ship to be within 5km of the location of the first ship.
    // First find the location of the first ship. We concat all the size classes, as we don't if any
    // size classes were empty for this battlefield.
    let _ship, sector, (x, y, z), _rotation = (List.concat [ xl; l; m; s ]).[0]
    // Now update every ships sector and location to be near the first ship, keeping other data the same.
    List.concat [
        [
            for (ship, _sector, _, rotation) in xl ->
                (ship, sector, (x + rand.Next(-spread, spread), y + rand.Next(-spread, spread), z + rand.Next(-spread, spread)), rotation)
        ]
        [
            for (ship, _sector, _, rotation) in l ->
                (ship, sector, (x + rand.Next(-spread, spread), y + rand.Next(-spread, spread), z + rand.Next(-spread, spread)), rotation)
        ]
        [
            for (ship, _sector, _, rotation) in m ->
                (ship, sector, (x + rand.Next(-spread, spread), y + rand.Next(-spread, spread), z + rand.Next(-spread, spread)), rotation)
        ]
        [
            for (ship, _sector, _, rotation) in s ->
                (ship, sector, (x + rand.Next(-spread, spread), y + rand.Next(-spread, spread), z + rand.Next(-spread, spread)), rotation)
        ]
    ]


// Some ships have custom loadouts, so we need a unique ID for them. Used by ProcessShip.
let loadoutUniqueId = makeIdGenerator ()

// Pick the equipment for grouped slots (turrets, shields): one pick per group.
// Assumes all slots in a group have identical tags, so picks equipment based on the first slot.
let loadoutGroups (ship: ShipInfo) =
    ship.EquipmentSlots
    |> shipEquipmentGroups
    |> List.sortBy (fun (groupName, _className) -> groupName)
    |> List.choose (fun ((groupName, className), slots) ->
        // printfn "GROUP for %s: %s, %s, %A" ship.Name groupName className slots[0].Tags

        slots[0].Tags
        |> tryPickEquipment
        |> Option.map (fun equipment -> {
            TagName = (if className = "shieldgenerator" then "shields" else className + "s")
            Macro = equipment.MacroName
            Group = groupName
            Exact = (if slots.Length > 1 then Some slots.Length else None)
        }))

let private macroLine (slot: ShipEquipmentSlot) (equipment: EquipmentInfo) = {
    Class = slot.Class
    SlotName = slot.Name
    Macro = equipment.MacroName
}

// Pick ONE piece of equipment shared by all the given slots (engines and shields of a
// ship should match, rather than being a random mix).
let private synchronizedMacroLines (slots: ShipEquipmentSlot list) =
    match slots with
    | [] -> []
    | first :: _ ->
        let equipment = pickEquipment first.Tags

        slots
        |> List.sortBy (fun x -> x.Name)
        |> List.map (fun slot -> macroLine slot equipment)

// Pick equipment for the ungrouped slots (main guns, etc.), each slot independently -
// EXCEPT for engines and shields: if there's more than one slot of them, ensure they're the same.
let loadoutMacros (ship: ShipInfo) =
    let ungroupedSlots =
        ship.EquipmentSlots
        |> List.filter (fun slot -> slot.Group.IsNone || slot.Class = "engine")

    let engineSlots = ungroupedSlots |> List.filter (fun s -> s.Class = "engine")

    let shieldSlots = ungroupedSlots |> List.filter (fun s -> s.Class = "shield")

    let otherSlots =
        ungroupedSlots
        |> List.filter (fun s -> s.Class <> "engine" && s.Class <> "shield")

    let engineLines = synchronizedMacroLines engineSlots
    let shieldLines = synchronizedMacroLines shieldSlots

    let otherLines =
        otherSlots
        |> List.sortBy (fun x -> x.Name)
        |> List.choose (fun slot -> tryPickEquipment slot.Tags |> Option.map (macroLine slot))

    List.concat [ engineLines; shieldLines; otherLines ]

// Determines if a ship needs a custom loadout (Boron or Terran L/XL Military) and composes it.
// Loadouts have ammunition and crew sections too (and wares?). We're not setting those currently.
// see loadouts.xml in various DLC for examples
//
// Loadouts are all or nothing per ship: we either hand build the entire loadout, or emit no
// loadout reference at all and the game generates the whole thing itself.
// As of X4 9.0, S/M equipment is unified under the 'advanced' compatibility tag, so the game
// can generate loadouts for any S/M ship natively, including Boron. We therefore only hand
// build the L/XL cases the game handles poorly: Boron L/XL, and Terran L/XL military (which
// otherwise couldn't be repaired/modified properly).
let MaybeCustomLoadout (shipName: string) =
    option {
        let! ship = findShipByMacroName shipName

        let isBoron = ship.MacroName.Contains "_bor_"
        let isTerran = ship.MacroName.Contains "_ter_" || ship.MacroName.Contains "_atf_"
        let isLargeOrXL = ship.Size = "ship_l" || ship.Size = "ship_xl"
        let isMilitary = militaryShips |> List.contains ship.MacroName

        // printfn "Ship: %s %s %b %b %b %b" shipName ship.Size isBoron isTerran isLargeOrXL isMilitary

        if (isBoron && isLargeOrXL) || (isTerran && isLargeOrXL && isMilitary) then
            printfn "    Generating CustomLoadout for %s" shipName
            let id = $"eod_abandoned_ship_loadout_{loadoutUniqueId ()}"

            // NOTE: field order matters - it is the seeded-random draw order
            // (macros, then groups, then the thruster), inherited from the old code.
            return {
                Id = id
                ShipMacro = ship.MacroName
                Macros = loadoutMacros ship
                Groups = loadoutGroups ship
                ThrusterMacro = (pickEquipment (Set.ofList [ "thruster"; ship.Thruster ])).MacroName
            }
    }

// Decide the details of an abandoned ship placement from the ship, sector, position
// and rotation given as parameters. The data layer writes the actual XML.
let ProcessShip ((ship, sector, (x, y, z), (yaw, pitch, roll)): ShipLocation) : AbandonedShip =
    // Interestingly, the units of KM and deg are specified in the XML attribute fields for abandoned ships.
    // I've not seen this elsewhere, and don't know if it's necessary, but for safety I'll duplicate it.
    printfn
        "GENERATING ABANDONED SHIP: %s, Sector: %s, Position: %A, Rotation: %A"
        ship
        sector
        (x, y, z)
        (yaw, pitch, roll)

    {
        Macro = ship
        Sector = sector
        PositionKm = (x, y, z)
        RotationDeg = (yaw, pitch, roll)
        Loadout = MaybeCustomLoadout ship
    }

// Create a list of random ships, assign them to random sectors, then generate XML that will place
// them as abandoned ships in the game.
// We don't want it completely random, as we want to make sure there's a good mix of ships in the game.
// We lean slighly towards generated economy ships vs military, though there's plenty of both.
// there should be, on average, one or two ships per sector.
let generate_abandoned_ships_file (placedObjectsFilename: string) (loadoutFilename: string) =
    let shipDirectives =
        [

            // A bunch of ships in unsafe space to begin
            generateRandomMilitaryAbandonedShips Tune.UnsafeMilitaryXL "xl" |> List.map ProcessShip
            generateRandomMilitaryAbandonedShips Tune.UnsafeMilitaryL "l" |> List.map ProcessShip
            generateRandomMilitaryAbandonedShips Tune.UnsafeMilitaryM "m" |> List.map ProcessShip
            generateRandomMilitaryAbandonedShips Tune.UnsafeMilitaryS "s" |> List.map ProcessShip
            generateRandomEconomyAbandonedShips Tune.UnsafeEconomyXL "xl" |> List.map ProcessShip
            generateRandomEconomyAbandonedShips Tune.UnsafeEconomyL "l" |> List.map ProcessShip
            generateRandomEconomyAbandonedShips Tune.UnsafeEconomyM "m" |> List.map ProcessShip
            generateRandomEconomyAbandonedShips Tune.UnsafeEconomyS "s" |> List.map ProcessShip


            // Lets generate a few battlefields of varying sizes
            Tune.Battlefields
            |> List.collect (fun (countXL, countL, countM, countS) ->
                generateBattlefield countXL countL countM countS |> List.map ProcessShip)

            // followed by a bunch of M & S in safe space.
            [
                for i in 1 .. Tune.SafeMilitaryM ->
                    militaryShips
                    |> filterListBy [ "m" ]
                    |> (generateRandomAbandonedShipFromListInSector (selectRandomSafeSector()))
                    |> ProcessShip
                for i in 1 .. Tune.SafeEconomyM ->
                    economyShips
                    |> filterListBy [ "m" ]
                    |> (generateRandomAbandonedShipFromListInSector (selectRandomSafeSector()))
                    |> ProcessShip
                for i in 1 .. Tune.SafeMilitaryS ->
                    militaryShips
                    |> filterListBy [ "s" ]
                    |> (generateRandomAbandonedShipFromListInSector (selectRandomSafeSector()))
                    |> ProcessShip
                for i in 1 .. Tune.SafeEconomyS ->
                    economyShips
                    |> filterListBy [ "s" ]
                    |> (generateRandomAbandonedShipFromListInSector (selectRandomSafeSector()))
                    |> ProcessShip

                // ok, a couple large l economy ship.
                for i in 1 .. Tune.SafeEconomyL ->
                    economyShips
                    |> filterListBy [ "l" ]
                    |> (generateRandomAbandonedShipFromListInSector (selectRandomSafeSector()))
                    |> ProcessShip

            ]

            // And finally a few individual ships
            [
                // Make sure there's at least one Raptor!
                filterBy [ "spl"; "xl"; "carrier" ]
                |> generateRandomAbandonedShipFromList
                |> ProcessShip
            ]
            [
                // And Asgard!
                filterBy [ "atf"; "xl"; "battleship" ]
                |> generateRandomAbandonedShipFromList
                |> ProcessShip
            ]
            [
                // And Syn.
                filterBy [ "atf"; "l"; "destroyer" ]
                |> generateRandomAbandonedShipFromList
                |> ProcessShip
            ]
            [
                // Guppy, because they're fun
                filterBy [ "bor"; "l"; "carrier" ]
                |> generateRandomAbandonedShipFromList
                |> ProcessShip
            ]

        // // Generate ships in specific sector to test loadouts for boron/terran
        // [
        //     filterBy [ "atf"; "xl"; "battleship" ]
        //     |> generateRandomAbandonedShipFromListInSector "Cluster_01_Sector002_macro"
        //     |> ProcessShip
        // ]
        // [
        //     filterBy [ "atf"; "l"; "destroyer" ]
        //     |> generateRandomAbandonedShipFromListInSector "Cluster_01_Sector002_macro"
        //     |> ProcessShip
        // ]
        // [
        //     filterBy [ "ter"; "l"; "destroyer" ]
        //     |> generateRandomAbandonedShipFromListInSector "Cluster_01_Sector002_macro"
        //     |> ProcessShip
        // ]
        // [
        //     filterBy [ "spl"; "l"; "destroyer" ]
        //     |> generateRandomAbandonedShipFromListInSector "Cluster_01_Sector002_macro"
        //     |> ProcessShip
        // ]
        // [
        //     filterBy [ "bor"; "xl"; "carrier" ]
        //     |> generateRandomAbandonedShipFromListInSector "Cluster_01_Sector002_macro"
        //     |> ProcessShip
        // ]
        // [
        //     filterBy [ "bor"; "l"; "miner" ]
        //     |> generateRandomAbandonedShipFromListInSector "Cluster_01_Sector002_macro"
        //     |> ProcessShip
        // ]


        ]
        |> List.concat

    writeAbandonedShips placedObjectsFilename loadoutFilename shipDirectives
