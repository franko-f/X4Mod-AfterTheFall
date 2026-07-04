/// <summary>
/// The pure domain types shared between the data layer and the logic modules.
///
/// The data layer parses the game's XML into these types, and accepts them (plus
/// the directive types added during the refactor) back to write the mod's XML.
/// Logic modules work exclusively in terms of the types in this file: they never
/// see XmlProvider types, XElement or XDocument.
/// </summary>
module X4.Types

open System.Xml.Linq

/// Opaque handle to a vanilla-parsed XML element. Only the data layer creates
/// these, and only the data layer's writer code may unwrap them (to clone the
/// exact parsed element, preserving attribute order and whitespace for
/// byte-identical output). Logic modules can hold and pass one along, but can
/// do nothing with it. Grep for "XmlSource.value" to audit every unwrap site.
type XmlSource =
    private
    | XmlSource of XElement

module XmlSource =
    let internal ofElement (element: XElement) = XmlSource element
    let value (XmlSource element) = element

/// A position in 3D space, in metres.
[<StructuredFormatDisplay("({X}, {Y}, {Z})")>]
type Position = {
    X: float
    Y: float
    Z: float
} with

    static member Default = { X = 0.0; Y = 0.0; Z = 0.0 }

[<StructuredFormatDisplay("({X}, {Y}, {Z}, {W})")>]
type Quaternion = {
    X: float
    Y: float
    Z: float
    W: float
} with

    static member Default = { X = 0.0; Y = 0.0; Z = 0.0; W = 1.0 }

/// The galaxy-map connection joining a gate to its remote counterpart.
type GateConnection = {
    Name: string
    Path: string option
    MacroPath: string option
}

/// A jump gate discovered in the zone files.
[<StructuredFormatDisplay("{Sector}/{Zone}:{Faction} ({ConnectionType})/{ConnectionName} {GateType} - {Position} rotation{Quarternion}")>]
type Gate = {
    Sector: string
    Zone: string // the zone macro name
    Faction: string
    GateType: string // the zone class
    ConnectionType: string
    ConnectionName: string
    Position: Position
    Quarternion: Quaternion
    Connection: GateConnection option
} with

    member gate.asString() =
        let connection =
            match gate.Connection with
            | None -> "Unknown"
            | Some connection -> connection.Name

        sprintf
            "%A/%A:%A (%A) %A %A - %A rotation: %A Connection: %A"
            gate.Sector
            gate.Zone
            gate.Faction
            gate.ConnectionType
            gate.ConnectionName
            gate.GateType
            gate.Position
            gate.Quarternion
            connection

/// A station entry from the vanilla god.xml (or a DLC god diff). Field list is
/// exactly what the logic layer reads; Source is the provider-parsed <station>
/// element, kept so the writer can clone it byte-identically.
type GodStation = {
    Id: string
    Race: string
    Owner: string
    Type: string option // "factory" | "tradingstation" | ...
    LocationClass: string option // "zone" | "sector" | ...
    LocationMacro: string option
    SelectTags: string option // <station><select tags="[defence]"/> - None when there is no <select>
    ConstructionPlan: string option // <station constructionplan="'ter_defence'">
    StationMacro: string option // <station macro="..."> - logging only
    Source: XmlSource
}

/// A product (factory quota) entry from the vanilla god.xml or a DLC god diff.
/// The mod's replacement quotas are built from scratch, so no Source is needed.
type GodProduct = {
    Id: string
    Owner: string
    Ware: string
    Type: string
    LocationFaction: string option
    QuotaGalaxy: int
    QuotaSector: int option
    QuotaCluster: int option
}
