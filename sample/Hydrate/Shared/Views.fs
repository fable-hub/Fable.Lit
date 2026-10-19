/// The view, written once.
///
/// Compiled by the .NET project against Fable.Lit.Server, and by Fable against the lit
/// bindings. Neither copy knows which it is; the only rule is that it may use nothing
/// that does not exist on both sides.
module Views

open Lit

type Item = { Name: string; Qty: int }

/// The data the page is rendered from. It has to be identical on both sides: markup
/// that hydrates cleanly but was built from different data leaves lit believing values
/// it never rendered.
let items =
    [ { Name = "Crate"; Qty = 40 }
      { Name = "Pallet"; Qty = 2 }
      { Name = "<script>alert('x')</script>"; Qty = 1 } ]

let row (item: Item) =
    html $"""<tr><td>{item.Name}</td><td>{item.Qty}</td></tr>"""

let page (count: int) (onClick: unit -> unit) =
    html
        $"""<div class="card">
              <h2>Packing list</h2>
              <table><tbody>{Lit.ofList (items |> List.map row)}</tbody></table>
              <p>Clicked <b class="count">{count}</b> times.</p>
              <button type="button" @click={Ev(fun _ -> onClick ())}>Click me</button>
            </div>"""
