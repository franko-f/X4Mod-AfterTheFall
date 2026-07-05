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

/// How a sector is represented in the vanilla mapdefaults file, which determines the
/// diff operation needed to add resource areas to it. Each case carries the
/// canonically cased macro name to use in the selector.
type MapDefaultsDatasetState =
    | HasResourceAreas of string // dataset exists and already has a <resourceareas> node
    // dataset exists with <properties> but no <resourceareas>. The <properties> children
    // are schema ordered (xs:sequence in libraries.xsd: boundaries, identification,
    // resources, resourceareas, sounds, area, ...), so a plain append would put our node
    // after sounds/area/access and fail validation. The second value is the existing
    // child to insert after (pos="after"), or None to prepend as the first child.
    | HasProperties of string * string option
    | NoDataset of string // the sector has no dataset in the file at all

// ==== WRITE-SIDE DIRECTIVES ====
// Logic modules produce these; the data layer's writers translate them to XML.

/// A faction's vanilla defence construction plan: the plan id that bastions are
/// stacked from, and the DLC (extension id, display name) providing it, if any.
type DefencePlanSource = {
    PlanId: string
    Dlc: (string * string) option
}

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

/// One quota scope of a jobs.xml job, named after the <quota> attributes.
[<RequireQualifiedAccess>]
type QuotaScope =
    | Galaxy
    | MaxGalaxy
    | Cluster
    | Sector
    | Wing

/// The quota of a jobs.xml job: just the scopes the vanilla file sets, in the
/// file's attribute order (galaxy, maxgalaxy, cluster, sector, wing).
type JobQuota = (QuotaScope * int) list

module JobQuota =
    /// The value of one scope of the quota, if the job sets it.
    let tryScope (scope: QuotaScope) (quota: JobQuota) =
        quota
        |> List.tryPick (fun (s, value) -> if s = scope then Some value else None)

type JobCategory = {
    Faction: string
    Tags: string // raw bracketed tag list, parsed on demand by Utilities.parseStringList
    Size: string option // ship_xl etc.
}

type JobLocation = {
    Class: string
    Macro: string option
    Faction: string option
    Relation: string option
    Comparison: string option
}

/// The <environment> element of a job. Source is the provider-parsed element:
/// the preferbuild rewrite clones it to preserve any other attributes byte-for-byte.
type JobEnvironment = {
    Preferbuilding: bool option
    Buildatshipyard: bool
    Source: XmlSource
}

/// A jobs.xml job entry (vanilla, core or DLC), flattened to the facts the job
/// processing logic reads.
type Job = {
    Id: string
    Category: JobCategory option
    Quota: JobQuota
    Location: JobLocation
    Environment: JobEnvironment option
    ShipSelectFaction: string option // flattens job.Ship.Select.Faction
    Task: string option // flattens job.Task.Task
    HasSubordinateModifier: bool // the job itself is an escort/subordinate
    SubordinateJobs: string[] option // the jobs of this job's escorts, if any
    Startactive: bool option
}

/// One equipment hardpoint on a ship hull, parsed from the ship's component
/// connections at load time.
type ShipEquipmentSlot = {
    Name: string
    Class: string // "weapon" | "turret" | "shield" | "engine" | "thruster"
    Size: string // small / medium / large / extralarge
    Group: string option
    Tags: Set<string>
}

/// A ship hull with its parsed equipment slots.
type ShipInfo = {
    Name: string
    MacroName: string
    Size: string // ship_s / ship_m / ship_l / ship_xl
    DLC: string
    Type: string
    Thruster: string // the thruster tag class the hull requires
    ComponentRef: string
    ComponentFile: string
    EquipmentSlots: ShipEquipmentSlot list
}

/// A piece of ship equipment (engine, shield, weapon, turret...).
type EquipmentInfo = {
    Name: string
    MacroName: string
    Class: string
    Size: string
    Tags: Set<string>
    ComponentName: string
}

/// Place a visual mining field region in a cluster map. The writer derives the
/// {Name}_connection / {Name}_macro element names from Name.
type RegionPlacement = {
    Name: string
    Cluster: string
    RegionRef: string
    Position: int * int * int
}

/// Grant one sector its X4 9.0 resource areas (the minable yields). State selects
/// which diff operation the writer emits; Areas are (composed sphere_* ref, amount).
type SectorResourceGrant = {
    State: MapDefaultsDatasetState
    Areas: (string * int) list
}

/// A change to a vanilla job, decided by the jobs logic and written by the data layer.
type JobDirective =
    /// Replace the job's <quota> element with these scaled scopes. The writer emits
    /// exactly these attributes, except galaxy, which is always written (0 if absent).
    | ReplaceJobQuota of jobId: string * quota: JobQuota
    /// Force the job to be built at shipyards instead of spawning at game start.
    /// Carries the whole job: the writer clones its existing <environment> (via
    /// Environment.Source) or adds a fresh one.
    | SetPreferBuild of job: Job

/// The Xenon station templates (mod_templates/object_templates.xml) that vanilla
/// stations get replaced with.
type XenonStationKind =
    | XenonShipyardStation
    | XenonWharfStation
    | XenonDefenceStation

/// What happens to a vanilla station the mod doesn't leave alone.
type StationAction =
    | RemoveOriginal
    | MoveOriginalTo of sector: string // emits two <replace> ops retargeting its location

/// The mod's decision for one vanilla god station.
type StationDirective = {
    Original: GodStation
    Replacement: XenonStationKind option // Xenon station placed at the original's location; None in neutral clusters
    Action: StationAction
}

/// A gate bastion station: the faction's defence station god entry cloned to a new
/// id/location, pointed at a stacked bastion construction plan.
type BastionStation = {
    BasedOn: GodStation
    Id: string
    ZoneClass: string
    ZoneName: string
    Position: Position
    PlanId: string // the atf_bastion_* construction plan to build
}

/// A new Xenon station placed from a template at a specific location.
type XenonTemplateStation = {
    Kind: XenonStationKind
    LocationClass: string
    LocationMacro: string
}

/// A new Xenon solar power plant product at a specific location.
type XenonSolarProduct = {
    LocationClass: string
    LocationMacro: string
}

/// Replace a vanilla product's quota in god.xml.
type ProductDirective = {
    ProductId: string
    QuotaType: string // "galaxy"
    Quota: int
}

/// A stacked bastion construction plan to generate into constructionplans.xml.
type BastionPlanDirective = {
    NewId: string
    SourcePlanId: string
    SourcePlan: XmlSource // the vanilla defence <plan> element (via X4.Data.Plans.loadDefencePlan)
    DlcPatch: (string * string) option
}

/// One <macros> line of a custom loadout: equipment mounted on a named slot.
type LoadoutMacroLine = {
    Class: string // element name: engine / shield / weapon / ...
    SlotName: string // the hardpoint connection name (path="../{SlotName}")
    Macro: string // the equipment macro to mount
}

/// One <groups> line of a custom loadout: equipment applied to a slot group.
type LoadoutGroupLine = {
    TagName: string // element name: turrets / shields / ...
    Macro: string
    Group: string
    Exact: int option // exact="{n}" attribute when the group has more than one slot
}

/// A complete hand-built ship loadout (Boron/Terran L-XL cases the game can't
/// generate well itself).
type ShipLoadout = {
    Id: string
    ShipMacro: string
    Macros: LoadoutMacroLine list
    Groups: LoadoutGroupLine list
    ThrusterMacro: string
}

/// An abandoned (claimable) ship to place in the galaxy.
type AbandonedShip = {
    Macro: string
    Sector: string
    PositionKm: int * int * int
    RotationDeg: int * int * int
    Loadout: ShipLoadout option // None = the game generates the loadout itself
}
