module X4.ProductQuotaInfo

open System.Xml.Linq
open FSharp.Data
open X4.Utilities
open X4.Data

// This module processes the product quotas (ie, the factory production modules they'll build),
// and allows us to view and analyse the quotas for factions. Used during mode development to
// help determine if there are differences or interpendancies between factions.


// This code is related to
type Quota = {
    Faction: string
    FactoryID: string
    Product: string
    QuotaGalaxy: int
    QuotaCluster: int
    QuotaSector: int
}
// type Product = { Faction:string; Product:string; QuotaGalaxy:int; QuotaCluster:int; QuotaSector:int}
type Products = Map<string, Quota> // Map of product ID to Quota. Each faction has an instance of this map containing it's product quotas
type FactionProductMap = Map<string, Products> // The map of all factions to their products & quotas.
module FactionProductMap = Map

// Give the name of a product and it's quota, look up the product in the products map and update it's quota
// if it exist. If it doesn't exist, add it to the map. Return the replacement Products
let updateProductQuota (quota: Quota) (products: Products) =
    match products.TryFind quota.Product with
    | None -> products |> Map.add quota.Product quota
    | Some productQuota ->
        // there was already a quote entry for this product, so we'll add up the quotas, and replace the entry.
        products
        |> Map.add quota.Product {
            productQuota with
                QuotaGalaxy = productQuota.QuotaGalaxy + quota.QuotaGalaxy
                QuotaCluster = productQuota.QuotaCluster + quota.QuotaCluster
                QuotaSector = productQuota.QuotaSector + quota.QuotaSector
        }

let addFactionProduct (quota: Quota) (factionProducts: FactionProductMap) =
    // Get the products map for the faction, or create a new one if it doesn't exist.
    let products =
        match factionProducts.TryFind quota.Faction with
        | None -> Map.empty
        | Some products -> products
    // And add the product to the faction, or increment the existing quotas for the product.
    factionProducts |> Map.add quota.Faction (products |> updateProductQuota quota)


// given a faction and a product/ware name, return the quota record as an Option
let getFactionProductCount (factionProducts: FactionProductMap) (faction: string) (product: string) =
    factionProducts.TryFind faction
    |> Option.bind (fun products -> products.TryFind product)

// Give a faction and a product, look up it's quote and return it as a formatted string,
// OR, return a default string of '---' if the quota doesn't exist for the product.
let getFactionProductCountAsString (factionProducts: FactionProductMap) (faction: string) (product: string) =
    getFactionProductCount factionProducts faction product
    |> Option.fold (fun s quota -> sprintf "%i/%i/%i" quota.QuotaGalaxy quota.QuotaCluster quota.QuotaSector) "---"


let printTableHeaders (factions: string list) =
    printfn "FACTION PRODUCTS TABLE (quotas are TotalInGalaxy/MaxPerSector)"
    printf "%20s" ""

    for faction in factions do
        printf "%10s" faction

    printfn ""

let printProductTable (factionProducts: FactionProductMap) (factions: string list) (products: string list) =
    printTableHeaders factions

    for product in products do
        printf "%20s" product

        for faction in factions do
            printf "%10s" (getFactionProductCountAsString factionProducts faction product)

        printfn ""

// Print out all products per faction in a table, so that we can easily analyse if factions are
// equal, or the default product quotas are designed to require trade between factions.
let printTable () =
    // Iterate through all products, building up a map of product to faction/quota. ie, a table of
    // rows = product, and columns = factions, and the cell is the quota. Store it in a map.

    let factionProducts =
        allProducts
        |> List.filter (fun product -> not (List.contains product.Owner [ "xenon"; "khaak" ])) // strip out some faction factories that we're not interested in
        |> List.map (fun product -> {
            // convert to a list of Quota records to make it easier to manipulate
            Faction = product.Owner
            FactoryID = product.Id
            Product = product.Ware
            QuotaGalaxy = product.Quota.Galaxy
            QuotaCluster = Option.defaultValue 0 product.Quota.Cluster
            QuotaSector = Option.defaultValue 0 product.Quota.Sector
        })
        |> List.fold
            (fun (factionProducts: FactionProductMap) (quota: Quota) -> addFactionProduct quota factionProducts)
            FactionProductMap.empty


    // First: Find all unique faction names:
    let factions =
        allProducts
        |> List.map (fun product -> product.Owner)
        |> List.distinct
        |> List.filter (fun faction -> not (List.contains faction [ "xenon"; "khaak" ]))

    // And all the unique product types
    let allProductTypes =
        allProducts |> List.map (fun product -> product.Ware) |> List.distinct

    // Then print a big table of this data!
    printProductTable factionProducts factions allProductTypes
