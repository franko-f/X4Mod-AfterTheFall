/// <summary>
/// The god.xml side of the data layer: the vanilla stations and products across the
/// core game and all DLC god diffs, exposed as the pure GodStation/GodProduct records,
/// plus the X4 9.0 prefab factory counts.
/// (Phase G of the refactor will also move the WRITING of the mod's god.xml here.)
/// </summary>
[<AutoOpen>]
module X4.Data.Economy

open System.Xml.Linq
open X4.Types
open X4.Utilities
open X4.Data.Xml
open X4.WriteModfiles // the diff-operation vocabulary the writer builds from

// Conversions from the provider types to the pure records in X4.Types.
// Source wraps the provider-parsed element itself - the writer clones it for
// byte-identical output, and its reference identity backs the station dedup
// behaviour in god.fs (see the T1 note there).
let private toGodStation (station: X4WorldStart.Station) : GodStation = {
    Id = station.Id
    Race = station.Race
    Owner = station.Owner
    Type = station.Type
    LocationClass = station.Location.Class
    LocationMacro = station.Location.Macro
    SelectTags = station.Station.Select |> Option.map (fun select -> select.Tags)
    ConstructionPlan = station.Station.Constructionplan
    StationMacro = station.Station.Macro
    Source = XmlSource.ofElement station.XElement
}

let private toGodProduct (product: X4WorldStart.Product) : GodProduct = {
    Id = product.Id
    Owner = product.Owner
    Ware = product.Ware
    Type = product.Type
    LocationFaction = product.Location.Faction
    QuotaGalaxy = product.Quota.Galaxy
    QuotaSector = product.Quota.Sector
    QuotaCluster = product.Quota.Cluster
}

// ===== 9.0 PREFAB FACTORY STATIONS =====
// X4 9.0 added static 'prefab' factory stations inside <god><products> (101 entries,
// 174 station instances; commonwealth factions in the core file, split/terran/boron in
// their DLC god diffs). They are galaxy scoped with relation="self", i.e. they spawn in
// sectors owned by their own faction, and they are ADDITIVE to the dynamic <product>
// quotas. The mod keeps them, but offsets each faction's dynamic product quota by the
// number of prefab factories of that ware (see X4.God.processProduct).
// Count station instances per (owner, ware), summing each entry's galaxy quota.
// Parsed with plain XDocument: the DLC god files are diffs, and the prefab stations sit
// inside their '<add sel="/god/products">' operations.
let prefabFactoryCounts: Map<string * string, int> =
    let prefabStationsIn (file: string) (isDiff: bool) =
        let doc = XDocument.Load file

        let stations =
            if isDiff then
                doc.Root.Elements(XName.Get "add")
                |> Seq.filter (fun add ->
                    match add.Attribute(XName.Get "sel") with
                    | null -> false
                    | sel -> sel.Value = "/god/products")
                |> Seq.collect (fun add -> add.Elements(XName.Get "station"))
            else
                doc.Root.Element(XName.Get "products").Elements(XName.Get "station")

        stations
        |> Seq.filter (fun s -> s.Attribute(XName.Get "id").Value.Contains "_prefab_")
        |> Seq.toList

    [
        yield! prefabStationsIn X4GodFileCore false
        for file in X4GodDlcFiles do
            yield! prefabStationsIn file true
    ]
    |> List.map (fun station ->
        let owner = station.Attribute(XName.Get "owner").Value
        let ware = station.Attribute(XName.Get "ware").Value
        // galaxy quota = how many instances of this station are placed at game start
        let instances =
            match station.Element(XName.Get "quotas").Element(XName.Get "quota").Attribute(XName.Get "galaxy") with
            | null -> 1
            | galaxy -> int galaxy.Value

        (owner, ware), instances)
    |> List.groupBy fst
    |> List.map (fun (key, entries) -> key, entries |> List.sumBy snd)
    |> Map.ofList

// Read all thge stations and products from the core game and the DLCs.
let allStations, allProducts =
    // Helper functions. Extract the stations from the 'add' section of a DLCs god diff/mod file.
    // While there are many 'add' sections, we're only interested in the one that that has the selectior '//god/stations'
    let getStationsFromDiff (diff: X4GodMod.Add[]) =
        let stationsAdd =
            Array.filter (fun (add: X4GodMod.Add) -> add.Sel = "/god/stations") diff

        [
            for stations in stationsAdd do
                for station in stations.Stations do
                    yield new X4WorldStart.Station(station.XElement)
        ]

    let getProductFromDiff (diff: X4GodMod.Add[]) =
        let productsAdd =
            Array.filter (fun (add: X4GodMod.Add) -> add.Sel = "/god/products") diff

        [
            for products in productsAdd do
                for product in products.Products do
                    yield new X4WorldStart.Product(product.XElement)
        ]

    let X4GodCore = X4WorldStart.Load(X4GodFileCore)
    let dlcGodMods = X4GodDlcFiles |> List.map X4GodMod.Load

    // Finally build up an uberlist of all our stations across all DLC and core game.
    // The DLC stations are of a different type: they're an XML DIFF file, not the GOD
    // file type. So we need to pull out the stations from the diff and convert them
    // to the same type as the core stations using the underlying XElement.
    let allStations =
        (Array.toList X4GodCore.Stations.Stations)
        @ (dlcGodMods |> List.collect (fun godMod -> getStationsFromDiff godMod.Adds))

    // Do the same for products.
    // 9.0: the <products> block also contains <station> prefab entries (70 faction factories,
    // new in 9.0), so the provider no longer collapses it to an array — the <product> children
    // are one level down. The prefab stations are NOT yet processed (see TODO.md): removing or
    // moving them needs selectors under //god/products/station, unlike regular stations.
    let allProducts =
        (Array.toList X4GodCore.Products.Products)
        @ (dlcGodMods |> List.collect (fun godMod -> getProductFromDiff godMod.Adds))

    allStations |> List.map toGodStation, allProducts |> List.map toGodProduct


// ==== WRITER ====
// The mod's god.xml is produced here from the pure directives decided by god.fs.
// The writers work on raw XElements (template clones and vanilla clones via
// XmlSource), built from the diff-operation vocabulary in X4.WriteModfiles.

// Attribute value of an element, or a fallback when the attribute is absent.
let private attrOr (fallback: string) (name: string) (el: XElement) =
    match el.Attribute(XName.Get name) with
    | null -> fallback
    | attr -> attr.Value

let private logAddStation (action: string) (station: XElement) =
    let attr name = station |> attrOr "none" name

    let tags =
        match station.Element(XName.Get "station") with
        | null -> "[none]"
        | spec ->
            match spec.Element(XName.Get "select") with
            | null -> "[none]"
            | select -> select |> attrOr "[none]" "tags"

    let location, locationMacro =
        match station.Element(XName.Get "location") with
        | null -> "none", "none"
        | location -> location |> attrOr "none" "class", location |> attrOr "none" "macro"

    printfn
        "   %s STATION %s race: %s, owner: %s, type: %s, location: %s:%s, id: %s, station: \"none\"   "
        action
        tags
        (attr "race")
        (attr "owner")
        (attr "type")
        location
        locationMacro
        (attr "id")

// Extract the Xenon stations from the GodModTemplate. We'll use these as templates when we add new xenon stations
let private X4ObjectTemplatesData = X4ObjectTemplates.Load(X4ObjectTemplatesFile)

let private xenonStationTemplate (kind: XenonStationKind) =
    let id =
        match kind with
        | XenonShipyardStation -> "shipyard_xenon_cluster"
        | XenonWharfStation -> "wharf_xenon_cluster"
        | XenonDefenceStation -> "xen_defence_cluster"

    (Array.find (fun (elem: X4ObjectTemplates.Station) -> elem.Id = id) X4ObjectTemplatesData.Stations)
        .XElement

// Render the Xenon station (if any) that takes over a processed station's location.
let private renderReplacementStation (directive: StationDirective) =
    match directive.Replacement with
    | None -> None
    | Some kind ->
        let station = directive.Original
        // create a new Xenon station from the template, retargeted to the original's location.
        let clone = new XElement(xenonStationTemplate kind)
        clone.SetAttributeValue(XName.Get "id", (clone |> attrOr "station" "id") + "_x_" + station.Id) // Give it a new unique ID
        let location = clone.Element(XName.Get "location")
        location.SetAttributeValue(XName.Get "class", Option.defaultValue "none" station.LocationClass)
        location.SetAttributeValue(XName.Get "macro", Option.defaultValue "none" station.LocationMacro)
        logAddStation "REPLACE" clone
        Some clone

// The remove operations clearing out the originals we don't move.
let private renderStationRemovals (directives: StationDirective list) = [
    for directive in directives do
        match directive.Action with
        | RemoveOriginal -> removeOp $"//god/stations/station[@id='{directive.Original.Id}']"
        | MoveOriginalTo _ -> ()
]

// The replace-operation pairs retargeting moved stations to their safe sectors.
let private renderStationMoves (directives: StationDirective list) = [
    for directive in directives do
        match directive.Action with
        | MoveOriginalTo randomSector ->
            yield replaceOp $"//god/stations/station[@id='{directive.Original.Id}']/location/@class" "sector"
            yield replaceOp $"//god/stations/station[@id='{directive.Original.Id}']/location/@macro" randomSector
        | RemoveOriginal -> ()
]

// Render a bastion station: clone the faction's defence station god entry and retarget
// its id, construction plan, location and position.
let private renderBastionStation (bastion: BastionStation) =
    let station = new XElement(XmlSource.value bastion.BasedOn.Source)
    station.SetAttributeValue(XName.Get "id", bastion.Id) // Give it a new unique ID

    // Point the bastion at our stacked construction plan instead of letting the
    // game resolve the faction's standard defence plan through the <select> tag.
    let stationSpec =
        match station.Element(XName.Get "station") with
        | null ->
            let spec = new XElement(XName.Get "station")
            station.Add spec
            spec
        | spec -> spec

    match stationSpec.Element(XName.Get "select") with
    | null -> ()
    | select -> select.Remove()

    stationSpec.SetAttributeValue(XName.Get "constructionplan", bastion.PlanId)

    // update location and set the location of the station copy to be the zone of the gate,
    let location = station.Element(XName.Get "location")
    location.SetAttributeValue(XName.Get "class", bastion.ZoneClass)
    location.SetAttributeValue(XName.Get "macro", bastion.ZoneName)
    location.SetAttributeValue(XName.Get "matchextension", "false") // without this, game ignores mods touching things outside their scope
    location.SetAttributeValue(XName.Get "solitary", null) // VIG faction has this attribute set that may cause station placement to fail

    // Now update the precise position within the zone to the caculated spot in a circle near the gate.
    let position =
        match station.Element(XName.Get "position") with
        | null ->
            printfn "   No position found for station %s" bastion.Id
            let posXml = new XElement(XName.Get "position")
            station.Add posXml
            posXml
        | position -> position

    position.SetAttributeValue(XName.Get "x", bastion.Position.X)
    position.SetAttributeValue(XName.Get "y", bastion.Position.Y)
    position.SetAttributeValue(XName.Get "z", bastion.Position.Z)

    logAddStation "ADDING" station
    station

// Render a new Xenon station placed from a template.
let private renderXenonStation (directive: XenonTemplateStation) =
    let station = new XElement(xenonStationTemplate directive.Kind)

    // Update the location and ID of our new station.
    let location = station.Element(XName.Get "location")
    location.SetAttributeValue(XName.Get "class", directive.LocationClass)
    location.SetAttributeValue(XName.Get "macro", directive.LocationMacro)
    station.SetAttributeValue(XName.Get "id", (station |> attrOr "" "id") + directive.LocationMacro) // Give it a new unique ID
    logAddStation "ADDING" station
    station

// Render a new Xenon solar power plant product.
let private renderSolarProduct (directive: XenonSolarProduct) =
    let locClass = directive.LocationClass
    let location = directive.LocationMacro
    let id = "xen_solar_" + location

    let product =
        new XElement(
            "product",
            new XAttribute("id", id),
            new XAttribute("ware", "energycells"),
            new XAttribute("owner", "xenon"),
            new XAttribute("type", "factory"),
            new XElement("quotas", new XElement("quota", new XAttribute("galaxy", 2), new XAttribute("sector", 2))),
            new XElement(
                "location",
                new XAttribute("class", locClass),
                new XAttribute("macro", location),
                new XAttribute("matchextension", "false")
            ),
            new XElement(
                "module",
                new XElement("select", new XAttribute("ware", "energycells"), new XAttribute("race", "xenon"))
            )
        )

    printfn "   ADDING PRODUCT xen_solar to %s:%s" locClass location
    product

// Construct an XML element representing a 'replace' tag that will replace a specific quota for a given product.
// example replace line:
// <replace sel="/god/products/product[@id='arg_graphene']/quotas/quota/@galaxy">18</replace>
let private product_replace_xml (id: string) (quota_type: string) (quota: int) =
    let xml =
        replaceOp $"//god/products/product[@id='{id}']/quotas/quota/@{quota_type}" quota

    printfn "  REPLACING PRODUCT %s with quota %s:%i using:\n %s" id quota_type quota (xml.ToString())
    xml

// Write the mod's god.xml from all the god directives, in the same order the original
// code assembled it: replacement adds, bastions, new xenon stations, xenon products,
// then the remove/replace operations appended to the diff root.
let writeGodFile
    (filename: string)
    (stationDirectives: StationDirective list)
    (bastions: BastionStation list)
    (xenonStations: XenonTemplateStation list)
    (solarProducts: XenonSolarProduct list)
    (productDirectives: ProductDirective list)
    =
    // The stations we're replacing with Xenon, followed by the new defense stations
    // near gates, then the extra Xenon wharfs/shipyards applying pressure in specific
    // sectors.
    let stationsAdd =
        addOp "//god/stations" [
            yield! stationDirectives |> List.choose renderReplacementStation
            yield! bastions |> List.map renderBastionStation
            yield! xenonStations |> List.map renderXenonStation
        ]

    let productsAdd =
        addOp "//god/products" (solarProducts |> List.map renderSolarProduct)

    // The two add sections first, then the 'remove' and 'replace' operations.
    let diff = newDiff ()
    diff.Add stationsAdd
    diff.Add productsAdd

    let changes =
        List.concat [
            renderStationRemovals stationDirectives
            [
                for directive in productDirectives ->
                    product_replace_xml directive.ProductId directive.QuotaType directive.Quota
            ]
            renderStationMoves stationDirectives
        ]

    for element in changes do
        diff.Add(element)

    X4.WriteModfiles.write_xml_file "core" filename diff
