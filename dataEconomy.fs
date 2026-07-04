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
open X4.Data.Xml

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
        yield! prefabStationsIn X4GodFileSplit true
        yield! prefabStationsIn X4GodFileTerran true
        yield! prefabStationsIn X4GodFilePirate true
        yield! prefabStationsIn X4GodFileBoron true
        yield! prefabStationsIn X4GodFileTimelines true
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
    let X4GodSplit = X4GodMod.Load(X4GodFileSplit)
    let X4GodTerran = X4GodMod.Load(X4GodFileTerran)
    let X4GodPirate = X4GodMod.Load(X4GodFilePirate)
    let X4GodBoron = X4GodMod.Load(X4GodFileBoron)
    let X4GodTimelines = X4GodMod.Load(X4GodFileTimelines)

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
            getStationsFromDiff X4GodTimelines.Adds
        ]

    // Do the same for products.
    // 9.0: the <products> block also contains <station> prefab entries (70 faction factories,
    // new in 9.0), so the provider no longer collapses it to an array — the <product> children
    // are one level down. The prefab stations are NOT yet processed (see TODO.md): removing or
    // moving them needs selectors under //god/products/station, unlike regular stations.
    let allProducts =
        List.concat [
            Array.toList X4GodCore.Products.Products
            getProductFromDiff X4GodSplit.Adds
            getProductFromDiff X4GodTerran.Adds
            getProductFromDiff X4GodPirate.Adds
            getProductFromDiff X4GodBoron.Adds
            getProductFromDiff X4GodTimelines.Adds
        ]

    allStations |> List.map toGodStation, allProducts |> List.map toGodProduct
