/// A page that exercises the React interop against the React the repo installs.
///
/// Not part of `npm test`, and not for want of trying: React ships CommonJS and reads
/// process.env at import time, the test runner serves ES modules to a browser that has
/// neither, and the rollup plugins that bridge that hand react-dom a React it cannot
/// find its own internals in. Vite does the same job correctly, so the check runs there.
///
///     npm run check:react     then open the page it prints
///
/// It renders a React component into a lit template, renders again with new props, and
/// leaves the findings on window.__result.
module Check

open Browser
open Fable.Core.JsInterop
open Fable.React
open Fable.React.Props
open Lit

let private badge (props: {| label: string |}) =
    span [ Class "badge" ] [ str props.label ]

let private badgeInLit: {| label: string |} -> TemplateResult = React.toLit (badge)

let private host = document.getElementById "app"

Lit.render host (html $"""<div class="wrap">{badgeInLit {| label = "one" |}}</div>""")

window.setTimeout(
    (fun () ->
        let first = host.querySelector ".badge"
        let firstContainer = host.querySelector ".wrap > div"
        Lit.render host (html $"""<div class="wrap">{badgeInLit {| label = "two" |}}</div>""")

        window.setTimeout(
            (fun () ->
                let second = host.querySelector ".badge"

                window?__result <-
                    {| rendered = not (isNull first)
                       firstText = (if isNull first then "" else first.textContent)
                       secondText = (if isNull second then "" else second.textContent)
                       sameBadge = obj.ReferenceEquals(first, second)
                       oldBadgeStillInTheDom = (not (isNull first)) && (first?isConnected: bool)
                       sameReactContainer = obj.ReferenceEquals(firstContainer, host.querySelector ".wrap > div") |}),
            200)
        |> ignore),
    200)
|> ignore
