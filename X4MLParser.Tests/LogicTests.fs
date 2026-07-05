/// Decision-level tests for the pure logic layer. These construct domain
/// records by hand (no XML, no baseline) and pin the RULES - multipliers,
/// exclusions, floors - rather than specific output values, so they survive
/// retuning of the constants in tuning.fs.
///
/// The last tests exercise the real data layer (they load the game files, so
/// the first one to run pays a few seconds of init).
module X4MLParser.Tests.LogicTests

open Xunit
open X4.Types

// ---------------------------------------------------------------------------
// Job processing rules (jobs.fs)
// ---------------------------------------------------------------------------

let private noQuota: JobQuota = {
    Galaxy = None
    Maxgalaxy = None
    Cluster = None
    Sector = None
    Wing = None
}

/// A minimal but complete job; tests override the fields they exercise.
let private makeJob id faction tags size quota : Job = {
    Id = id
    Category = Some { Faction = faction; Tags = tags; Size = size }
    Quota = quota
    Location = {
        Class = "galaxy"
        Macro = None
        Faction = Some faction
        Relation = None
        Comparison = None
    }
    Environment = None
    ShipSelectFaction = Some faction
    Task = None
    HasSubordinateModifier = false
    SubordinateJobs = None
    Startactive = None
}

let private scaledBy multiplier value =
    Some(int (ceil (float value * multiplier)))

[<Fact>]
let ``xenon military XL quotas scale by the XL multiplier, rounding up`` () =
    let job =
        makeJob "t_xen_xl" "xenon" "[military]" (Some "ship_xl") {
            noQuota with
                Galaxy = Some 10
                Cluster = Some 3
        }

    match X4.Jobs.processJob job with
    | Some(ReplaceJobQuota(id, quota)) ->
        Assert.Equal("t_xen_xl", id)
        Assert.Equal(scaledBy X4.Tuning.Jobs.XenonMilitaryXLMultiplier 10, quota.Galaxy)
        Assert.Equal(scaledBy X4.Tuning.Jobs.XenonMilitaryXLMultiplier 3, quota.Cluster)
        Assert.Equal(None, quota.Maxgalaxy)
    | other -> failwith $"expected ReplaceJobQuota, got %A{other}"

[<Fact>]
let ``wing quotas are never scaled or emitted`` () =
    let job =
        makeJob "t_wing" "xenon" "[military]" (Some "ship_s") {
            noQuota with
                Galaxy = Some 4
                Wing = Some 2
        }

    match X4.Jobs.processJob job with
    | Some(ReplaceJobQuota(_, quota)) ->
        Assert.Equal(None, quota.Wing)
        Assert.True(quota.Galaxy.IsSome)
    | other -> failwith $"expected ReplaceJobQuota, got %A{other}"

[<Fact>]
let ``a job with only a wing quota produces no directive`` () =
    let job =
        makeJob "t_wing_only" "xenon" "[military]" (Some "ship_s") { noQuota with Wing = Some 2 }

    Assert.Equal(None, X4.Jobs.processJob job)

[<Fact>]
let ``subordinate jobs are left alone`` () =
    let job =
        { makeJob "t_sub" "xenon" "[military]" (Some "ship_s") { noQuota with Galaxy = Some 5 } with
            HasSubordinateModifier = true }

    Assert.Equal(None, X4.Jobs.processJob job)

[<Fact>]
let ``jobs not active at game start are left alone`` () =
    let job =
        { makeJob "t_inactive" "xenon" "[military]" (Some "ship_s") { noQuota with Galaxy = Some 5 } with
            Startactive = Some false }

    Assert.Equal(None, X4.Jobs.processJob job)

[<Fact>]
let ``mass traffic jobs are left alone`` () =
    let job =
        { makeJob "t_police" "xenon" "[military]" (Some "ship_s") { noQuota with Galaxy = Some 5 } with
            Task = Some "masstraffic.police" }

    Assert.Equal(None, X4.Jobs.processJob job)

[<Fact>]
let ``pirate military quotas are reduced by the pirate multiplier`` () =
    let job =
        makeJob "t_pirate" "scaleplate" "[military]" (Some "ship_m") { noQuota with Galaxy = Some 10 }

    match X4.Jobs.processJob job with
    | Some(ReplaceJobQuota(_, quota)) ->
        Assert.Equal(scaledBy X4.Tuning.Jobs.PirateMilitaryMultiplier 10, quota.Galaxy)
    | other -> failwith $"expected ReplaceJobQuota, got %A{other}"

[<Fact>]
let ``faction civilian jobs are left alone`` () =
    let job =
        makeJob "t_civ" "argon" "[trade]" (Some "ship_l") { noQuota with Galaxy = Some 10 }

    Assert.Equal(None, X4.Jobs.processJob job)

[<Fact>]
let ``faction small military ships are left alone`` () =
    let job =
        makeJob "t_small" "argon" "[military]" (Some "ship_m") { noQuota with Galaxy = Some 10 }

    Assert.Equal(None, X4.Jobs.processJob job)

[<Fact>]
let ``faction capital military jobs become build-at-shipyard`` () =
    let job =
        makeJob "t_cap" "argon" "[military]" (Some "ship_xl") { noQuota with Galaxy = Some 10 }

    match X4.Jobs.processJob job with
    | Some(SetPreferBuild j) -> Assert.Equal("t_cap", j.Id)
    | other -> failwith $"expected SetPreferBuild, got %A{other}"

// ---------------------------------------------------------------------------
// Product quota rules (god.fs) - these touch prefabFactoryCounts, so the data
// layer loads on first use; fake wares keep the expectations data-independent.
// ---------------------------------------------------------------------------

let private makeProduct owner ware quota : GodProduct = {
    Id = "t_product"
    Owner = owner
    Ware = ware
    Type = "factory"
    LocationFaction = None
    QuotaGalaxy = quota
    QuotaSector = None
    QuotaCluster = None
}

[<Fact>]
let ``xenon production is multiplied`` () =
    match X4.God.processProduct (makeProduct "xenon" "energycells" 7) with
    | Some directive ->
        Assert.Equal(7 * X4.Tuning.Economy.XenonProductionRatio, directive.Quota)
        Assert.Equal("galaxy", directive.QuotaType)
    | None -> failwith "expected a directive for xenon"

[<Fact>]
let ``khaak products are untouched`` () =
    Assert.Equal(None, X4.God.processProduct (makeProduct "khaak" "energycells" 5))

[<Fact>]
let ``faction production is reduced by the production ratio`` () =
    // a ware with no 9.0 prefab factories, so no prefab offset applies
    match X4.God.processProduct (makeProduct "argon" "t_fake_ware" 100) with
    | Some directive ->
        Assert.Equal(int (ceil (100.0 * X4.Tuning.Economy.ProductionRatio)), directive.Quota)
    | None -> failwith "expected a directive for argon"

[<Fact>]
let ``reduced quotas never drop below the minimum`` () =
    match X4.God.processProduct (makeProduct "argon" "t_fake_ware" 1) with
    | Some directive -> Assert.Equal(X4.Tuning.Economy.MinimumProductQuota, directive.Quota)
    | None -> failwith "expected a directive for argon"

// ---------------------------------------------------------------------------
// Gate defence geometry (gates.fs) - pure quaternion/ring maths
// ---------------------------------------------------------------------------

[<Fact>]
let ``defence stations ring the gate at the configured distance`` () =
    let gate = {
        Sector = "t_sector"
        Zone = "t_zone"
        Faction = "argon"
        GateType = "zone"
        ConnectionType = "gates"
        ConnectionName = "t_conn"
        Position = { X = 1000.0; Y = 200.0; Z = -500.0 }
        Quaternion = Quaternion.Default
        Connection = None
    }

    let count = X4.Tuning.GateDefence.StationsPerGate
    let distance = X4.Tuning.GateDefence.StationDistance
    let locations = X4.Gates.getDefenseStationLocations gate count distance

    Assert.Equal(count, locations.Length)

    for position, _ in locations do
        let dx = position.X - gate.Position.X
        let dy = position.Y - gate.Position.Y
        let dz = position.Z - gate.Position.Z
        let actual = sqrt (dx * dx + dy * dy + dz * dz)
        Assert.InRange(actual, float distance - 1.0, float distance + 1.0)

    // all ring positions must be distinct
    let distinct = locations |> List.map fst |> List.distinct
    Assert.Equal(locations.Length, distinct.Length)

// ---------------------------------------------------------------------------
// Integration: the real data layer + decision aggregates
// ---------------------------------------------------------------------------

[<Fact>]
let ``jobs load from the core game and all DLCs`` () =
    Assert.True(X4.Data.JobData.allJobs.Length > 100, $"only {X4.Data.JobData.allJobs.Length} jobs loaded")

[<Fact>]
let ``every gate bastion is backed by a generated construction plan`` () =
    let planDirectives, bastions = X4.God.gateBastions ()

    Assert.True(bastions.Length > 0, "no bastions generated")
    Assert.Equal(0, bastions.Length % X4.Tuning.GateDefence.StationsPerGate)

    let planIds = planDirectives |> List.map (fun plan -> plan.NewId) |> Set.ofList

    for bastion in bastions do
        Assert.Contains(bastion.PlanId, planIds)
