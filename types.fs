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
