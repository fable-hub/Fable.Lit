/// Adopts the page the server sent.
module App

open Browser
open Lit

// The div the server rendered into, not the card inside it: the root part markers sit
// around the card, so this is the element that contains them.
let private container = document.getElementById "app" :> Browser.Types.Element

// State the client owns from here on. It starts where the server left it, which is the
// contract: hydration is lit being told "this markup is that template with this data".
let mutable private count = 0

let rec private show () =
    Lit.render container (Views.page count bump)

and private bump () =
    count <- count + 1
    // An ordinary render from here on. The nodes are lit's now.
    show ()

// The one call that matters. Adopts if it can, renders if it cannot, and says so in the
// console either way.
Hydrate.adopt container (Views.page count bump)

console.log ("hydrated; the button below is live and the table was never re-rendered")
