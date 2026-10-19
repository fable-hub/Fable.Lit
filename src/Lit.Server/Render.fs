/// Renders Fable.Lit templates on .NET.
///
/// The same `html $"..."` view function compiles two ways: by Fable, against the real
/// bindings, into lit templates in the browser; and by the .NET compiler, against this,
/// into HTML. A project references one or the other, never both -- they declare the
/// same `Lit` namespace on purpose, so a shared view file keeps `open Lit` and needs no
/// conditional compilation.
///
/// Nothing here reimplements what HtmlTypeProvider already does. Escaping, the Node
/// model and the composition point are all its: a rendered template is a `Node`, which
/// is precisely what a page template's hole accepts, so an island's markup drops into a
/// server-rendered page without either side handling strings.
///
/// Two kinds of output. `render` and `toNode` write plain HTML, which the browser then
/// renders over. `renderHydratable` and `toHydratableNode` also write the marker
/// comments lit's SSR writes (`<!--lit-part DIGEST-->`, `<!--lit-node N-->`), so lit's
/// client-side `hydrate()` from `@lit-labs/ssr-client` can adopt the markup in place
/// instead of rebuilding it: the elements stay, and the bindings are attached to them.
namespace Lit

open System
open System.Text
open System.Text.RegularExpressions
open HtmlTypeProvider

/// A template and the values that fill its holes. Under Fable this same source produces
/// lit's own TemplateResult instead.
type TemplateResult =
    internal
        { Segments: string[]
          Values: obj[]
          /// 0 an ordinary template, 1 `Lit.nothing`, 2 a sequence from `Lit.ofList`.
          ///
          /// Explicit because hydration needs to know which of the three a value is, and
          /// they are indistinguishable by shape: `Lit.ofList` is a TemplateResult here
          /// and a plain iterable under Fable, and lit wraps an iterable in a bare part
          /// marker rather than one carrying a digest.
          Kind: int }

/// An event handler on its way into a template. The server drops it: a handler is a
/// closure over client state and there is nothing to serialise. Named to match the
/// Fable side so a shared view file type-checks against either.
type EvHandler = EvHandler of obj

/// Raised when a template uses something this renderer cannot honour.
///
/// Loudly, and never by guessing. A directive such as `styleMap` or `until` does its
/// work inside lit at commit time, and a renderer that quietly emitted an approximation
/// would ship subtly wrong markup that no test would catch. Refusing is the only honest
/// answer until each one is implemented deliberately.
exception UnsupportedTemplateValue of message: string

[<AutoOpen>]
module Server =

    /// A hole preceded by `name=`, `@name=`, `?name=` or `.name=`.
    ///
    /// This is the whole game. lit decides what a binding *is* from the literal text in
    /// front of the hole, and here that text is ours both to read and to rewrite: the
    /// sigil has to come out of the output, or the browser is served a literal
    /// `@click=""` attribute that means nothing to anyone.
    let private binding =
        Regex(@"(?<sigil>[@?.])?(?<name>[A-Za-z_:][-\w:.]*)\s*=\s*$", RegexOptions.Compiled)

    /// Rebuilds the literal segments from `FormattableString.Format`.
    ///
    /// Fable's FormattableString has `GetStrings()` and hands lit the segments directly;
    /// .NET's does not, so "a{0}b" is re-split here, honouring the `{{` and `}}` escapes
    /// that an interpolated string uses for literal braces.
    let internal splitFormat (format: string) =
        let segments = ResizeArray<string>()
        let current = StringBuilder()
        let mutable i = 0

        while i < format.Length do
            match format.[i] with
            | '{' when i + 1 < format.Length && format.[i + 1] = '{' ->
                current.Append('{') |> ignore
                i <- i + 2
            | '{' ->
                let close = format.IndexOf('}', i)

                if close < 0 then
                    failwithf "Unterminated hole in template: %s" format

                segments.Add(current.ToString())
                current.Clear() |> ignore
                i <- close + 1
            | '}' when i + 1 < format.Length && format.[i + 1] = '}' ->
                current.Append('}') |> ignore
                i <- i + 2
            | c ->
                current.Append(c) |> ignore
                i <- i + 1

        segments.Add(current.ToString())
        segments.ToArray()

    /// Interprets a template literal as HTML.
    let html (fmt: FormattableString) : TemplateResult =
        { Segments = splitFormat fmt.Format
          Values = fmt.GetArguments()
          Kind = 0 }

    /// Where a hole sits, and which node it belongs to.
    type internal Hole =
        /// In text position. lit puts a comment marker here, so it occupies a node.
        | ChildHole
        /// In a tag, binding an attribute of the element at this node index.
        | AttrHole of elementIndex: int
        /// Inside a quoted attribute value that is already open, as in `title="a {x}"`.
        /// lit supports this; the value goes in as text, with no name to write.
        | QuotedHole of elementIndex: int

    /// What a template looks like to lit's own template walker.
    ///
    /// Hydration needs node indices: a `<!--lit-node N-->` marker tells lit which
    /// element carries attribute bindings, and N is the position of that element in a
    /// depth-first walk of the parsed template counting *elements and comments only*,
    /// text excluded. lit computes it with a TreeWalker over the HTML it generates, so
    /// the two counts must agree exactly.
    ///
    /// They agree because the same three things add a node on both sides: an element, a
    /// literal comment, and a child binding (lit writes a comment marker where the value
    /// goes). A closing tag adds nothing. An attribute binding adds nothing either; it
    /// only names the element being opened.
    ///
    /// Getting this wrong is quieter than getting the digest wrong. A digest mismatch
    /// throws; an index mismatch makes hydrate `break` out of its loop
    /// (hydrate-lit-html.js:285), so the element's attribute and event parts are never
    /// created and its handlers silently never fire.
    type internal Analysis =
        { Holes: Hole[]
          /// Where to write a `<!--lit-node N-->` marker: segment, offset, index.
          NodeMarkers: (int * int * int) list
          /// Whether the template contains a `<template>` element. lit counts one as a
          /// single node and never walks into its content, so past that point the count
          /// kept here and lit's own are no longer the same count.
          HasTemplateElement: bool }

    let private rawTextTags =
        System.Collections.Generic.HashSet<string>(
            [ "script"; "style"; "textarea"; "title" ],
            StringComparer.OrdinalIgnoreCase)

    /// Walks the literal segments the way an HTML parser would, to place every hole and
    /// count every node.
    ///
    /// A regex on the tail of a segment was enough to tell an attribute binding from a
    /// text one, and is not enough for this: a `>` inside a quoted attribute value would
    /// convince it that a tag had ended. So this tracks the same states lit's own
    /// scanner does -- text, inside a tag, inside a quoted value, inside a comment, and
    /// inside a raw-text element -- and refuses the cases it does not model rather than
    /// producing an index that is quietly wrong.
    let internal analyze (segments: string[]) : Analysis =
        let holes = ResizeArray<Hole>()
        let markers = ResizeArray<int * int * int>()

        // Depth-first count of elements and comments, which is what lit's walker counts.
        let mutable nodeIndex = -1
        // The element currently being opened, and where its `<` is.
        let mutable current = -1
        let mutable currentAt = (0, 0)
        let mutable currentMarked = false

        let mutable state = 0 // 0 text, 1 in tag, 2 in quoted value, 3 in comment, 4 raw text
        let mutable quote = '"'
        let mutable rawTag = ""
        // Whether the tag being read is a closing one. Without it, the `>` of `</style>`
        // looks exactly like the `>` of `<style>` and puts the scanner back into raw
        // text, so everything after the block goes uncounted and every later binding
        // appears to be inside it.
        let mutable closing = false
        let mutable sawTemplate = false

        for segIndex in 0 .. segments.Length - 1 do
            let s = segments.[segIndex]
            let mutable i = 0

            while i < s.Length do
                match state with
                | 0 ->
                    if i + 3 < s.Length && s.[i] = '<' && s.[i + 1] = '!' && s.[i + 2] = '-' && s.[i + 3] = '-' then
                        nodeIndex <- nodeIndex + 1
                        state <- 3
                        i <- i + 4
                    elif i + 1 < s.Length && s.[i] = '<' && s.[i + 1] = '/' then
                        // A closing tag is not a node.
                        state <- 1
                        closing <- true
                        current <- -1
                        i <- i + 2
                    elif i + 1 < s.Length && s.[i] = '<' && Char.IsLetter s.[i + 1] then
                        nodeIndex <- nodeIndex + 1
                        current <- nodeIndex
                        currentAt <- (segIndex, i)
                        currentMarked <- false

                        let nameEnd =
                            let mutable j = i + 1

                            while j < s.Length && (Char.IsLetterOrDigit s.[j] || s.[j] = '-') do
                                j <- j + 1

                            j

                        rawTag <- s.Substring(i + 1, nameEnd - i - 1)

                        if String.Equals(rawTag, "template", StringComparison.OrdinalIgnoreCase) then
                            sawTemplate <- true

                        state <- 1
                        closing <- false
                        i <- nameEnd
                    else
                        i <- i + 1
                | 1 ->
                    match s.[i] with
                    | '"'
                    | '\'' ->
                        quote <- s.[i]
                        state <- 2
                        i <- i + 1
                    | '>' ->
                        state <-
                            if closing then 0
                            elif rawTextTags.Contains rawTag then 4
                            else 0

                        closing <- false
                        i <- i + 1
                    | _ -> i <- i + 1
                | 2 ->
                    if s.[i] = quote then state <- 1
                    i <- i + 1
                | 3 ->
                    if i + 2 < s.Length && s.[i] = '-' && s.[i + 1] = '-' && s.[i + 2] = '>' then
                        state <- 0
                        i <- i + 3
                    else
                        i <- i + 1
                | _ ->
                    // Raw text: nothing in here starts a node until the element closes.
                    if i + 1 < s.Length && s.[i] = '<' && s.[i + 1] = '/' then
                        state <- 1
                        closing <- true
                        current <- -1
                        i <- i + 2
                    else
                        i <- i + 1

            // The hole that follows this segment, if there is one.
            if segIndex < segments.Length - 1 then
                match state with
                | 0 ->
                    // lit writes a comment marker where the value goes, so it counts.
                    nodeIndex <- nodeIndex + 1
                    holes.Add ChildHole
                | 1 ->
                    if current < 0 then
                        raise (UnsupportedTemplateValue "A binding inside a closing tag is not supported.")

                    if not currentMarked then
                        let seg, off = currentAt
                        markers.Add(seg, off, current)
                        currentMarked <- true

                    holes.Add(AttrHole current)
                | 2 ->
                    if current < 0 then
                        raise (UnsupportedTemplateValue "A binding inside a closing tag is not supported.")

                    if not currentMarked then
                        let seg, off = currentAt
                        markers.Add(seg, off, current)
                        currentMarked <- true

                    holes.Add(QuotedHole current)
                | 3 -> raise (UnsupportedTemplateValue "A binding inside an HTML comment is not supported.")
                | _ ->
                    raise (
                        UnsupportedTemplateValue
                            $"A binding inside a <{rawTag}> element is not supported: its content is raw text, which lit parses differently."
                    )

        { Holes = holes.ToArray()
          NodeMarkers = List.ofSeq markers
          HasTemplateElement = sawTemplate }

    /// The node indices of the elements carrying attribute bindings, in template order.
    /// Exposed so a test can hold it against lit's own walk.
    let internal attributeElementIndices (t: TemplateResult) =
        (analyze t.Segments).NodeMarkers |> List.map (fun (_, _, index) -> index) |> Array.ofList

    /// lit's digest of a template, computed from the same segments lit hashes.
    ///
    /// This is the identifier lit writes into a `<!--lit-part ...-->` marker and checks
    /// when hydrating, and it is a port of `digestForTemplateResult` in
    /// @lit-labs/ssr-client. The algorithm is deliberately portable -- its own comment
    /// lists "easily specifiable and implementable in multiple languages" as a goal --
    /// but three details decide whether a port is exact or merely usually right:
    ///
    ///  * `charCodeAt` returns a UTF-16 code unit and the loop runs to `s.length`, so a
    ///    character outside the BMP is hashed as its two surrogate halves. .NET's char
    ///    is also UTF-16, so iterating chars matches; iterating runes would not.
    ///  * `(h * 33) ^ c` is evaluated as a double, coerced to int32 by the xor, then
    ///    back to uint32 by the store. The product is small enough to be exact in a
    ///    double, and xor is bitwise, so wrapping uint32 arithmetic gives the same bits.
    ///  * The hashes are then read as raw bytes, little-endian, one Latin-1 character
    ///    each, and base64'd. The byte order is written out here rather than left to
    ///    BitConverter, which follows the machine.
    ///
    /// Verified against lit's own function in the browser rather than against this
    /// description: see the digest case in test/DifferentialTest.fs.
    let internal digestOf (segments: string[]) =
        let hashes = [| 5381u; 5381u |]

        for s in segments do
            for i in 0 .. s.Length - 1 do
                hashes.[i % 2] <- (hashes.[i % 2] * 33u) ^^^ uint32 s.[i]

        let bytes =
            [| for h in hashes do
                   byte (h &&& 0xFFu)
                   byte ((h >>> 8) &&& 0xFFu)
                   byte ((h >>> 16) &&& 0xFFu)
                   byte ((h >>> 24) &&& 0xFFu) |]

        Convert.ToBase64String bytes

    /// lit's digest of this template.
    let digest (t: TemplateResult) = digestOf t.Segments

    /// The single value `Lit.nothing` hands out.
    ///
    /// One instance rather than a fresh record per access, so it can be recognised by
    /// reference when markers are emitted. It matters: on the Fable side `Lit.nothing`
    /// is lit's own sentinel, which hydrate treats as a leaf and expects a bare
    /// `<!--lit-part-->` for. Emitting a digest marker for it, as would happen if it
    /// were an ordinary template, is a mismatch and a thrown error.
    let internal nothingSentinel: TemplateResult =
        { Segments = [| "" |]
          Values = [||]
          Kind = 1 }

    /// What to say when a `Node` turns up where a value was expected.
    ///
    /// Everything this file hands out is a `Node` and a hole takes any object, so nothing
    /// stops one being passed back in. Without a case of its own it fell through to
    /// `string`, and the page was served the words "HtmlTypeProvider.Node".
    ///
    /// Refused rather than written out raw, because a view is the same code the browser
    /// runs and there is no `Node` there: whatever went in here, lit would have nothing
    /// to put in the same place.
    let private nodeInTemplate (where: string) =
        $"A Node cannot {where} an html template. Compose the other way round: the template's Node goes into the page's hole (toNode, toHydratableNode), not a Node into the template's."

    /// The text a value contributes in text position.
    ///
    /// Through `Node.Text`, so the encoding is HtmlTypeProvider's rather than a second
    /// opinion about it. This matters more than it looks: lit builds a *text node* for a
    /// text binding, so markup in a value can never be parsed as HTML on the client. A
    /// server renderer that emitted it raw would not be slightly different, it would be
    /// a hole in the site.
    let rec private textNode (hydratable: bool) (value: obj) : Node =
        match value with
        | null -> Node.Empty()
        | :? EvHandler -> Node.Empty()
        | :? Node -> raise (UnsupportedTemplateValue(nodeInTemplate "fill a hole in"))
        | :? TemplateResult as t when t.Kind = 2 ->
            // A sequence: each item is a child part of its own inside the iterable's.
            match t.Values.[0] with
            | :? seq<TemplateResult> as items -> textNode hydratable (box items)
            | _ -> Node.Empty()
        | :? TemplateResult as t -> toNodeCore hydratable false t
        | :? seq<TemplateResult> as items ->
            items
            |> Seq.map (fun item ->
                if not hydratable then
                    toNodeCore false false item
                else
                    Node.Fragment
                        [ Node.RawHtml(childMarker (box item))
                          toNodeCore true false item
                          Node.RawHtml "<!--/lit-part-->" ])
            |> Node.Fragment
        | :? string as s -> Node.Text s
        | :? bool as b -> Node.Text(if b then "true" else "false")
        | v -> Node.Text(string v)

    /// What hydrate() expects around a value in text position.
    ///
    /// A bare `<!--lit-part-->` for anything it treats as a leaf, and a marker carrying
    /// the template's digest when the value is itself a template, because that is what
    /// it compares against on the way in. An iterable opens its own part and each item
    /// opens one inside it, which is the shape openChildPart builds when it pushes an
    /// 'iterable' state.
    and private childMarker (value: obj) =
        match value with
        // A template carries its digest. `nothing` and a sequence do not: lit sees the
        // first as a leaf and the second as an iterable, and gives both a bare marker.
        | :? TemplateResult as t when t.Kind = 0 -> $"<!--lit-part {digestOf t.Segments}-->"
        | _ -> "<!--lit-part-->"

    /// The template as a `Node`.
    ///
    /// One code path for both kinds of output, because two would drift: the markers are
    /// the only difference between HTML a browser shows and HTML lit can adopt, and a
    /// second renderer that emitted them would eventually disagree with this one about
    /// something else.
    and internal toNodeCore (hydratable: bool) (isRoot: bool) (t: TemplateResult) : Node =
        let parts = ResizeArray<Node>()

        // The scanner runs for every render, not only the hydratable one, because it is
        // what says whether a hole is in text or in a tag. Deciding that from the tail of
        // the literal alone cannot work: prose ends in `word = ` exactly as an attribute
        // does, and `<p>total = {n}</p>` was being served as `<p>total="5"</p>`.
        let analysis = analyze t.Segments

        // Only where lit will adopt the result, which is where the two counts have to
        // agree. Written as plain HTML a template element is just markup, with no index
        // to get wrong and no second renderer to disagree with.
        //
        // Refused on the element rather than on whether a binding happens to follow it.
        // With none it would hydrate, and then mean something else the first time lit
        // rendered the same view itself: the page's parser attaches a declarative shadow
        // root, and lit, which builds its DOM from an inert template, never does.
        if hydratable && analysis.HasTemplateElement then
            raise (
                UnsupportedTemplateValue
                    "A <template> element cannot be part of markup lit is going to adopt. lit counts it as one node and never looks inside, so every binding after it would be looked for on the wrong node and silently never made; and a shadow root written this way is attached by the page's parser but not by lit when it renders the same view."
            )

        // Node markers have to be written in front of the element they name, so their
        // positions are worked out before anything is emitted.
        let markersBySegment =
            if not hydratable then
                dict []
            else
                analysis.NodeMarkers
                |> List.groupBy (fun (segment, _, _) -> segment)
                |> List.map (fun (segment, group) ->
                    segment, group |> List.map (fun (_, offset, index) -> offset, index) |> List.sortBy fst)
                |> dict

        // Only the outermost template opens a part of its own. A nested one is already
        // inside the marker its hole wrote, and a second would be an extra part that
        // hydrate is not expecting.
        if hydratable && isRoot then
            parts.Add(Node.RawHtml $"<!--lit-part {digestOf t.Segments}-->")

        for i in 0 .. t.Segments.Length - 1 do
            let segment = t.Segments.[i]

            // The segment, with a `<!--lit-node N-->` written in front of every element
            // that carries attribute bindings.
            let emitSegment (upTo: int) =
                let inserts =
                    match markersBySegment.TryGetValue i with
                    | true, list -> list |> List.filter (fun (offset, _) -> offset < upTo)
                    | _ -> []

                let mutable last = 0

                for offset, index in inserts do
                    parts.Add(Node.RawHtml(segment.Substring(last, offset - last)))
                    parts.Add(Node.RawHtml $"<!--lit-node {index}-->")
                    last <- offset

                parts.Add(Node.RawHtml(segment.Substring(last, upTo - last)))

            if i >= t.Values.Length then
                emitSegment segment.Length
            else
                let value = t.Values.[i]

                // What kind of hole this is comes from the scanner. The regex is only
                // asked for the attribute's name and sigil afterwards, and only where a
                // name is expected.
                let m = binding.Match(segment)

                // The escaped text a value contributes where a name has already been
                // written, or where the quote is already open.
                let attributeText () =
                    match value with
                    | null -> ()
                    | :? EvHandler -> ()
                    | :? string as str -> parts.Add(Node.Text str)
                    | :? bool as b -> parts.Add(Node.RawHtml(if b then "true" else "false"))
                    | :? TemplateResult ->
                        raise (UnsupportedTemplateValue "A nested template cannot be an attribute value.")
                    | :? Node -> raise (UnsupportedTemplateValue(nodeInTemplate "be an attribute value in"))
                    | v -> parts.Add(Node.Text(string v))

                match analysis.Holes.[i] with
                | ChildHole ->
                    emitSegment segment.Length

                    if hydratable then
                        parts.Add(Node.RawHtml(childMarker value))

                    parts.Add(textNode hydratable value)

                    if hydratable then
                        parts.Add(Node.RawHtml "<!--/lit-part-->")

                // The quote is already open, as in `title="a {x}"`, so the value goes in
                // as it stands and there is no name to write.
                | QuotedHole _ ->
                    emitSegment segment.Length
                    attributeText ()

                // In a tag with no `name=` in front of it: an element binding, as in
                // `<div {Lit.refValue r}>`. A ref is a handle on a live node, so the
                // server has nothing to write, exactly as for an event handler.
                | AttrHole _ when not m.Success -> emitSegment segment.Length

                | AttrHole _ ->
                    let lead = segment.Substring(0, m.Index)
                    let name = m.Groups.["name"].Value

                    match m.Groups.["sigil"].Value with
                    // A listener and a property have no HTML form at all, so the binding
                    // leaves with its value. TrimEnd, or dropping it strands the space
                    // that separated it from the attribute before.
                    | "@"
                    | "." -> emitSegment (lead.TrimEnd().Length)

                    // A boolean attribute is present or absent. There is no `false`.
                    | "?" ->
                        match value with
                        | :? bool as on when on ->
                            // The space in front of it separated it from the attribute
                            // before, and is wanted.
                            emitSegment lead.Length
                            parts.Add(Node.RawHtml name)
                        // Absent, and so is the space that led to it, or the element
                        // renders as `<button >`.
                        | :? bool -> emitSegment (lead.TrimEnd().Length)
                        | v ->
                            emitSegment lead.Length

                            raise (
                                UnsupportedTemplateValue
                                    $"?{name} expects a bool, got {v.GetType().Name}. In lit a boolean attribute is present or absent."
                            )

                    | _ ->
                        emitSegment lead.Length
                        parts.Add(Node.RawHtml(name + "=\""))

                        match value with
                        | null -> ()
                        | :? EvHandler ->
                            raise (UnsupportedTemplateValue $"An event handler cannot be an attribute value ({name}).")
                        | :? TemplateResult ->
                            raise (
                                UnsupportedTemplateValue
                                    $"A nested template cannot be an attribute value ({name})."
                            )
                        | :? Node -> raise (UnsupportedTemplateValue(nodeInTemplate $"be an attribute value ({name}) in"))
                        // Node.Text encodes quotes as well as angle brackets, so it is
                        // safe in an attribute, not only in text.
                        | :? string as s -> parts.Add(Node.Text s)
                        | :? bool as b -> parts.Add(Node.RawHtml(if b then "true" else "false"))
                        | v -> parts.Add(Node.Text(string v))

                        parts.Add(Node.RawHtml "\"")

        if hydratable && isRoot then
            parts.Add(Node.RawHtml "<!--/lit-part-->")

        Node.Fragment parts

    /// The template as a `Node`, ready to compose into a page.
    let toNode (t: TemplateResult) = toNodeCore false true t

    /// The template as a `Node` lit can adopt.
    ///
    /// Opt-in, and off by default, because the markers are an internal protocol of an
    /// experimental package: emit them and the client must call `hydrate` with the same
    /// template and data, or throw.
    let toHydratableNode (t: TemplateResult) = toNodeCore true true t

    /// The template as an HTML string.
    let render (t: TemplateResult) =
        let sb = StringBuilder()
        (toNode t).Invoke(sb)
        sb.ToString()

    /// The template as an HTML string lit can adopt.
    let renderHydratable (t: TemplateResult) =
        let sb = StringBuilder()
        (toHydratableNode t).Invoke(sb)
        sb.ToString()

    // Below: the template-facing surface a shared view file needs in order to compile on
    // .NET. Each either does the same work as its Fable counterpart, or stands in for
    // something the server has no use for.

    /// Wrapper for event handlers to help type checking. Dropped when rendered.
    let inline Ev (handler: 'E -> unit) = EvHandler(box handler)

    /// The signatures below are not a design: they are copied from the Fable side, and
    /// have to stay copied. A shared view file compiles against whichever of the two is
    /// referenced, so any drift here is a file that builds in the browser and not on the
    /// server, or worse, builds on both and means different things.
    type Lit =
        /// Renders nothing. A TemplateResult like any other, because that is how it is
        /// typed on the Fable side: `if x then html $"..." else Lit.nothing` must compile.
        static member nothing: TemplateResult = nothingSentinel

        /// Joins the classes whose flag is set. Pure string work on both sides, so this
        /// one is genuinely the same function rather than a stand-in.
        static member classes(classes: (string * bool) seq) =
            classes |> Seq.filter snd |> Seq.map fst |> String.concat " "

        static member classes(classes: string seq) = classes |> String.concat " "

        /// Renders a list of templates in order.
        ///
        /// Returns a TemplateResult rather than the sequence, because that is what it
        /// returns under Fable, where it is `unbox items`. Here it is a template with a
        /// single hole holding the sequence, which the renderer already knows how to
        /// walk.
        static member ofList(items: TemplateResult list) : TemplateResult =
            { Segments = [| ""; "" |]
              Values = [| box (items :> seq<TemplateResult>) |]
              Kind = 2 }
