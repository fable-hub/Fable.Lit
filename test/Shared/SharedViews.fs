/// Views compiled twice: by Fable against the lit bindings, and by the .NET compiler
/// against Lit.Server. Neither copy knows which it is.
///
/// This file is the contract. It may only use the surface both sides implement --
/// `html`, `Ev`, `Lit.classes`, `Lit.ofList`, `Lit.nothing` -- and nothing from
/// Browser.Types, no hooks, no `document`. If it stops compiling on one side, the two
/// APIs have drifted, which is the thing the differential test exists to catch.
module SharedViews

open Lit

type Item = { Name: string; Qty: int }

let icon (name: string) =
    html $"""<svg class="icon"><use href={"#" + name}></use></svg>"""

let toolbar (maximised: bool) (busy: bool) =
    html
        $"""<div class="grid-toolbar"><button type="button" class={Lit.classes [ "panel-size", true; "on", maximised ]} ?disabled={busy} title={if maximised then "Exit" else "Full screen"} @click={Ev(ignore)}>{icon (if maximised then "i-min" else "i-max")}</button></div>"""

let row (item: Item) =
    html $"""<tr><td>{item.Name}</td><td>{item.Qty}</td></tr>"""

let private emptyNote = html $"""<p class="hint">Nothing yet</p>"""

let grid (items: Item list) =
    // The empty note is bound above rather than written inline: a triple-quoted string
    // cannot appear inside an interpolation hole of another one.
    let note = if items.IsEmpty then emptyNote else Lit.nothing

    html
        $"""<div class="card">{toolbar false true}<table class="item-table"><tbody>{Lit.ofList (items |> List.map row)}</tbody></table>{note}</div>"""

/// Node indices are counted over elements and comments, and these two exist to prove
/// that the counting is right rather than merely plausible: the bound element sits after
/// a closing tag in one and after a comment in the other, so an index that counted
/// closing tags, or skipped comments, would be caught here and nowhere else.
let afterClosingTag (cls: string) =
    html $"""<div><span>first</span><b class={cls}>second</b></div>"""

let afterComment (cls: string) =
    html $"""<div><!-- a note --><i class={cls}>text</i></div>"""

/// A view with a handler, for the adoption test: the click is what proves lit created
/// the event part rather than merely leaving the DOM alone.
let clickable (onClick: unit -> unit) =
    html $"""<div class="wrap"><button type="button" class="act" @click={Ev(fun _ -> onClick ())}>Press</button></div>"""

/// The cases both runtimes render, by name. The differential test walks this list, so
/// adding one here covers it on both sides at once.
let cases: (string * TemplateResult) list =
    [ "icon", icon "i-cube"
      "toolbar-plain", toolbar false false
      "toolbar-maximised-busy", toolbar true true
      "row", row { Name = "Crate"; Qty = 40 }
      // The value that decides whether the two agree about safety, not just about shape.
      "row-with-markup", row { Name = "<script>alert('x')</script>"; Qty = 1 }
      "grid-empty", grid []
      "grid", grid [ { Name = "Crate"; Qty = 40 }; { Name = "Pallet"; Qty = 2 } ]
      "after-closing-tag", afterClosingTag "wide"
      "after-comment", afterComment "tall"
      "clickable", clickable ignore ]
