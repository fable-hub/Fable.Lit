/// Telling a rendered tree that it has gone.
///
/// Components know when they leave the page: a LitElement gets disconnectedCallback, a
/// HookComponent disposes what `Hook.useEffectOnce` handed back. Markup rendered into a
/// plain element knows nothing, and whatever it started goes on running long after the
/// element is out of the document, because nothing ever said otherwise.
///
/// lit's answer is the root part, which `render` returns and Fable.Lit used to discard.
module DisconnectTest

open System
open Browser
open Fable.Core
open Fable.Core.JsInterop
open Lit
open WebTestRunner

let private host () =
    let el = document.createElement "div"
    document.body.appendChild el |> ignore
    el

describe "Disconnecting" <| fun () ->

    // lit's own `ref` is an AsyncDirective, so it is told, and tells the callback by
    // handing it nothing. That makes it a usable proof that the message arrives.
    it "reaches the directives in a tree that was rendered into a plain element" <| fun () -> promise {
        let el = host ()
        let mutable attached = false

        let part =
            Lit.renderPart
                el
                (html $"""<p {Lit.refCallback (fun (found: Browser.Types.Element option) -> attached <- found.IsSome)}>here</p>""")

        do! Promise.sleep 50

        if not attached then
            failwith "the ref never saw the element, so this proves nothing about disconnecting"

        part.setConnected false
        do! Promise.sleep 50

        if attached then
            failwith "the tree was told it is disconnected and its directives were not"

        document.body.removeChild el |> ignore
    }

    it "tells directives when lit clears them, and not when the DOM is torn out from under it" <| fun () -> promise {
        let el = host ()
        let mutable attached = false

        let marked =
            html $"""<p {Lit.refCallback (fun (found: Browser.Types.Element option) -> attached <- found.IsSome)}>here</p>"""

        // Rendered inside a hole, so the next render can take it away.
        Lit.render el (html $"""<div class="slot">{marked}</div>""")
        do! Promise.sleep 50

        if not attached then
            failwith "the ref never saw the element"

        // lit removes it: told.
        Lit.render el (html $"""<div class="slot">{Lit.nothing}</div>""")
        do! Promise.sleep 50

        if attached then
            failwith "lit cleared the part and the directive in it was never told"

        // Now the other half. Put it back, then remove the whole container the way any
        // code outside lit would.
        Lit.render el (html $"""<div class="slot">{marked}</div>""")
        do! Promise.sleep 50

        if not attached then
            failwith "the ref did not come back when lit rendered it again"

        document.body.removeChild el |> ignore
        do! Promise.sleep 50

        if not attached then
            failwith "lit noticed a plain DOM removal, which would mean this whole API is unnecessary"
    }

    // Borrowing the browser's own answer. A tag with a dash can be upgraded to a custom
    // element, and custom elements are the only things the browser tells about joining
    // and leaving the document -- so an island wrapped in one gets what a LitElement has,
    // without being a LitElement.
    it "tracks connection through a host element, both ways" <| fun () -> promise {
        // Set up on arrival, torn down on departure, and the pairing is the assertion:
        // nothing here can record a teardown that was never set up.
        let seen = ResizeArray<string>()

        let subscription =
            Lit.trackConnection (
                "island-host",
                fun _ ->
                    seen.Add "up"

                    { new IDisposable with
                        member _.Dispose() = seen.Add "down" }
            )

        let holder = host ()
        holder.innerHTML <- """<island-host id="tracked"></island-host>"""

        let tracked = holder.querySelector "#tracked"
        let mutable attached = false

        Lit.render
            tracked
            (html $"""<p {Lit.refCallback (fun (found: Browser.Types.Element option) -> attached <- found.IsSome)}>here</p>""")

        do! Promise.sleep 50

        if not attached then
            failwith "the ref never saw the element"

        // Out of the document: the host is told, and passes it on.
        holder.removeChild tracked |> ignore
        do! Promise.sleep 50

        if attached then
            failwith "the host left the document and what it rendered was never told"

        // Back in: an element that was moved should come back with everything it had.
        holder.appendChild tracked |> ignore
        do! Promise.sleep 50

        if not attached then
            failwith "the host came back and what it rendered stayed disconnected"

        if List.ofSeq seen <> [ "up"; "down"; "up" ] then
            failwith $"""the handler ran {List.ofSeq seen}, not [up; down; up]"""

        // Disposing stops the listening and tears down what is still up. The element
        // cannot be undefined, and the tree it holds still pauses and resumes.
        subscription.Dispose()

        if List.ofSeq seen <> [ "up"; "down"; "up"; "down" ] then
            failwith $"""disposing did not tear down what was still set up ({List.ofSeq seen})"""

        attached <- true
        holder.removeChild tracked |> ignore
        do! Promise.sleep 50

        if List.ofSeq seen <> [ "up"; "down"; "up"; "down" ] then
            failwith $"""a disposed subscription still ran ({List.ofSeq seen})"""

        if attached then
            failwith "disposing the handler stopped the forwarding as well"

        document.body.removeChild holder |> ignore
    }

    // The other order, and the one a server-rendered page is always in: the element is
    // in the document before anything has asked to be told about it. Defining a tag
    // upgrades what is already there, on the spot, and the handler has to hear about
    // that arrival like any other. Otherwise what it sets up is not set up until the
    // element has left and come back, and its first departure has nothing to dispose.
    it "tells a handler about an element that was in the page before it asked" <| fun () -> promise {
        let seen = ResizeArray<string>()

        let holder = host ()
        holder.innerHTML <- """<island-early id="early"></island-early>"""
        let early = holder.querySelector "#early"

        let subscription =
            Lit.trackConnection (
                "island-early",
                fun _ ->
                    seen.Add "up"

                    { new IDisposable with
                        member _.Dispose() = seen.Add "down" }
            )

        do! Promise.sleep 50

        if List.ofSeq seen <> [ "up" ] then
            failwith $"""an element already in the page was reported as {List.ofSeq seen}, not [up]"""

        holder.removeChild early |> ignore
        do! Promise.sleep 50

        if List.ofSeq seen <> [ "up"; "down" ] then
            failwith $"""its first departure was reported as {List.ofSeq seen}"""

        holder.appendChild early |> ignore
        do! Promise.sleep 50

        if List.ofSeq seen <> [ "up"; "down"; "up" ] then
            failwith $"""its return was reported as {List.ofSeq seen}"""

        subscription.Dispose()
        document.body.removeChild holder |> ignore
    }

    // And the same for a tag that is already being tracked, where there is no upgrade to
    // hear: the element arrived long ago, and a second handler is as entitled to know it
    // is there as the first.
    it "tells a later handler about elements that are already being tracked" <| fun () -> promise {
        let seen = ResizeArray<string>()

        Lit.trackConnection "island-settled"

        let holder = host ()
        holder.innerHTML <- """<island-settled id="settled"></island-settled>"""
        let settled = holder.querySelector "#settled"
        do! Promise.sleep 50

        let subscription =
            Lit.trackConnection (
                "island-settled",
                fun _ ->
                    seen.Add "up"

                    { new IDisposable with
                        member _.Dispose() = seen.Add "down" }
            )

        do! Promise.sleep 50

        if List.ofSeq seen <> [ "up" ] then
            failwith $"""an element that was already tracked was reported as {List.ofSeq seen}, not [up]"""

        holder.removeChild settled |> ignore
        do! Promise.sleep 50

        if List.ofSeq seen <> [ "up"; "down" ] then
            failwith $"""its departure was reported as {List.ofSeq seen}"""

        subscription.Dispose()
        document.body.removeChild holder |> ignore
    }

    // Adoption that lit declines without throwing.
    //
    // An empty container, or markup rendered without the markers, is reported to the
    // console and nowhere else: hydrate returns as though it had worked and leaves the
    // container as it found it. Anything relying on the exception to notice sees a blank
