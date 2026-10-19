/// Does the .NET renderer agree with lit?
///
/// The only honest way to ask is in a browser. `Lit.Server` produces a string and lit
/// produces a DOM, so the comparison parses the server's string into a DOM too and
/// compares what the browser makes of each. That normalises everything neither side
/// should be judged on: attribute quoting, the serialisation of a bare boolean
/// attribute, the order the parser hands things back.
///
/// lit's own marker comments are removed first. They are how lit finds its bindings
/// again on the next render, they are absent by design from server output, and leaving
/// them in would mean measuring them rather than the markup.
///
/// The expectations come from test/server-rendered.json, written by the .NET test
/// project from the same SharedViews.fs this file compiles. So `dotnet test` has to run
/// before `npm test`; if the file is missing, this fails rather than passes quietly.
module DifferentialTest

open Browser
open Fable.Core
open Fable.Core.JsInterop
open Elmish
open Lit
open Lit.Elmish
open Expect
open WebTestRunner

[<Emit("fetch($0).then(r => r.ok ? r.json() : Promise.reject(new Error('missing ' + $0)))")>]
let private fetchJson (url: string) : JS.Promise<obj> = jsNative

/// Comment nodes out, at every depth.
let rec private stripComments (node: Browser.Types.Node) =
    let children = [ for i in 0 .. int node.childNodes.length - 1 -> node.childNodes.[i] ]

    for child in children do
        if child.nodeType = 8.0 then
            node.removeChild child |> ignore
        else
            stripComments child

/// What lit makes of a template, with its bookkeeping removed.
let private fromLit (template: TemplateResult) =
    let host = document.createElement "div"
    document.body.appendChild host |> ignore
    Lit.render host template
    stripComments host
    let html = host.innerHTML
    document.body.removeChild host |> ignore
    html

/// What the browser makes of a piece of server markup.
///
/// Parsed inside a <template>, not a <div>, and the difference is not academic: the
/// HTML parser discards <tr> and <td> that are not inside a table, so a row rendered
/// into a div comes back as bare text. A template element parses fragments as written,
/// which is also how lit parses its own templates -- so this compares like with like.
let private fromServer (html: string) =
    let host = document.createElement "template"
    host.innerHTML <- html
    stripComments (host?content: Browser.Types.Node)
    host.innerHTML

/// lit's own digest function, the one hydrate() checks a `<!--lit-part ...-->` marker
/// against. Imported rather than reimplemented here on purpose: the point of the test is
/// that the F# port agrees with this, so this side must be lit's.
[<Import("digestForTemplateResult", "@lit-labs/ssr-client")>]
let private digestForTemplateResult (t: TemplateResult) : string = jsNative

/// lit's own internals, used the way lit-ssr uses them.
///
/// `_getTemplateHtml` turns a template's strings into the HTML lit parses, with a
/// comment marker where each child binding goes and a `$lit$` suffix on every bound
/// attribute name. Walking that HTML the way lit does gives the node indices a
/// `<!--lit-node N-->` marker has to carry, which is the ground truth the .NET scanner
/// is measured against.
[<Import("hydrate", "@lit-labs/ssr-client")>]
let private hydrate (value: obj) (container: Browser.Types.Node) (options: obj option) : unit = jsNative

[<Import("_$LH", "lit-html")>]
let private litInternals: obj = jsNative

/// The node indices of elements carrying attribute bindings, as lit itself would count
/// them: a depth-first walk showing elements and comments only, text excluded.
let private litAttributeNodeIndices (strings: string[]) =
    let html: obj = litInternals?_getTemplateHtml (strings, 1)
    let suffix: string = litInternals?_boundAttributeSuffix

    let host = document.createElement "template"
    // getTemplateHtml returns [html, attrNames]; the html is a TrustedHTML-ish wrapper.
    host.innerHTML <- string (html?(0))

    let walker = document?createTreeWalker (host?content, 129)
    let found = ResizeArray<int>()
    let mutable index = -1

    let mutable node = walker?nextNode ()

    while not (isNull node) do
        index <- index + 1

        if node?nodeType = 1 then
            let attrs = node?attributes
            let mutable bound = false

            for i in 0 .. (attrs?length: int) - 1 do
                if (attrs?(i)?name: string).EndsWith suffix then bound <- true

            if bound then found.Add index

        node <- walker?nextNode ()

    found.ToArray()

describe "Differential" <| fun () ->
    it "the .NET renderer agrees with lit on every shared view" <| fun () -> promise {
        let! expected = fetchJson "/test/server-rendered.json"

        for name, template in SharedViews.cases do
            let rendered = fromLit template
            let served = fromServer (expected?(name): string)

            // Named in the failure, or a mismatch in the seventh case reads as a
            // mismatch in nothing at all.
            if rendered <> served then
                failwith $"{name}\n  lit:    {rendered}\n  server: {served}"
    }

    // The node indices a <!--lit-node N--> marker must carry, checked against lit's own
    // count. A wrong index does not throw: hydrate breaks out of its loop and the
    // element's attribute and event parts are never created, so the handlers simply
    // never fire. That is the failure this test exists to make loud.
    it "the .NET node indices match lit's own" <| fun () -> promise {
        let! expected = fetchJson "/test/server-rendered.json"

        for name, template in SharedViews.cases do
            let fromLit = litAttributeNodeIndices (template?strings) |> Array.map string |> String.concat ","
            let fromServer: string = expected?(name + "#nodes")

            if fromLit <> fromServer then
                failwith $"{name}\n  lit:    [{fromLit}]\n  server: [{fromServer}]"
    }

    // The one that decides whether any of this works: does lit actually adopt the
    // server's markup?
    //
    // Adoption is not observable by looking at the HTML -- markup that was re-rendered
    // looks identical to markup that was kept. So the test holds on to the element node
    // before hydrating and asserts it is the *same object* afterwards, and then clicks
    // the button to prove the event part was created. A wrong node index does not throw:
    // hydrate breaks out of its loop and the handlers simply never fire, which is
    // exactly what the click catches.
    it "lit adopts the server's markup and wires its handlers" <| fun () -> promise {
        let! expected = fetchJson "/test/server-rendered.json"

        let mutable clicked = 0
        let template = SharedViews.clickable (fun () -> clicked <- clicked + 1)

        let host = document.createElement "div"
        document.body.appendChild host |> ignore
        host.innerHTML <- (expected?("clickable#hydratable"): string)

        let before = host.querySelector "button"

        if isNull before then
            failwith "the server markup should contain the button"

        hydrate (box template) (host :> Browser.Types.Node) None

        let after = host.querySelector "button"

        if not (obj.ReferenceEquals(before, after)) then
            failwith "hydrate replaced the button instead of adopting it"

        (after :?> Browser.Types.HTMLElement).click()
        do! Promise.sleep 0

        if clicked <> 1 then
            failwith $"the handler did not fire after hydration (clicked = {clicked}); the event part was never created"

        document.body.removeChild host |> ignore
    }

    // The failure path, which is the one that matters most: it runs when something is
    // already wrong, and it must not make things worse. lit's render does not clear the
    // container it is given, so a fallback that simply renders would leave the server's
    // markup in place and append its own underneath it.
    it "a refused adoption leaves one copy of the page, not two" <| fun () -> promise {
        let! expected = fetchJson "/test/server-rendered.json"

        let host = document.createElement "div"
        document.body.appendChild host |> ignore
        // Server markup for one template...
        host.innerHTML <- (expected?("toolbar-plain#hydratable"): string)

        // ...adopted with a different one. The digest will not match, hydrate throws,
        // and Hydrate.adopt falls back to rendering.
        Hydrate.adopt host (SharedViews.icon "i-cube")
        do! Promise.sleep 0

        let buttons = host.querySelectorAll("button").length
        let svgs = host.querySelectorAll("svg").length
        document.body.removeChild host |> ignore

        if buttons <> 0 then
            failwith $"the server's markup survived the fallback: {buttons} button(s) left beside the new render"

        if svgs <> 1 then
            failwith $"expected exactly one rendered icon, found {svgs}"
    }

    // Adoption that lit declines without throwing.
    //
    // An empty container, or markup rendered without the markers, is reported to the
    // console and nowhere else: hydrate returns as though it had worked and leaves the
    // container as it found it. Anything relying on the exception to notice sees a blank
    // island and a message nobody reads.
    it "renders when there is nothing to adopt, rather than leaving the container empty" <| fun () -> promise {
        let el = document.createElement "div"
        document.body.appendChild el |> ignore
        let mutable clicks = 0

        Hydrate.adopt el (SharedViews.clickable (fun () -> clicks <- clicks + 1))
        do! Promise.sleep 50

        let button = el.querySelector "button.act"

        if isNull button then
            failwith "nothing was rendered into a container that had nothing to adopt"

        (button :?> Browser.Types.HTMLElement).click ()
        do! Promise.sleep 50

        if clicks <> 1 then
            failwith "what was rendered is not wired up"

        document.body.removeChild el |> ignore
    }

    // Elmish over server-rendered markup. The point is the first render: it adopts the
    // DOM the server sent instead of replacing it, and everything after it is an
    // ordinary Elmish render into DOM lit owns by then.
    it "an Elmish program adopts the server's markup and then drives it" <| fun () -> promise {
        let! expected = fetchJson "/test/server-rendered.json"

        let host = document.createElement "div"
        host.id <- "elmish-host"
        document.body.appendChild host |> ignore
        host.innerHTML <- (expected?("counter#hydratable"): string)

        let before = host.querySelector "button"

        let init () = { SharedViews.Count = 0 }, Cmd.none

        let update (msg: SharedViews.Msg) (model: SharedViews.Model) =
            match msg with
            | SharedViews.Increment -> { model with SharedViews.Count = model.Count + 1 }, Cmd.none

        Program.mkProgram init update SharedViews.counter
        |> Program.withLitHydratedOnElement host
        |> Program.run

        do! Promise.sleep 0

        let after = host.querySelector "button"

        if not (obj.ReferenceEquals(before, after)) then
            failwith "the Elmish program replaced the server's button instead of adopting it"

        (after :?> Browser.Types.HTMLElement).click()
        do! Promise.sleep 50

        let shown = (host.querySelector ".n").textContent

        if shown <> "1" then
            failwith $"dispatch did not reach the view after hydration (shows {shown})"

        if not (obj.ReferenceEquals(before, host.querySelector "button")) then
            failwith "the update rebuilt the DOM instead of patching the adopted nodes"

        document.body.removeChild host |> ignore
    }

    // The port of lit's digest, checked against lit's digest rather than against a
    // description of it. A wrong digest is not a rendering difference: hydrate() throws
    // on a mismatch, so this is the test standing between a port and an exception in
    // somebody's production browser.
    it "the .NET digest matches lit's own" <| fun () -> promise {
        let! expected = fetchJson "/test/server-rendered.json"

        for name, template in SharedViews.cases do
            let fromLit = digestForTemplateResult template
            let fromServer: string = expected?(name + "#digest")

            if fromLit <> fromServer then
                failwith $"{name}\n  lit:    {fromLit}\n  server: {fromServer}"
    }
