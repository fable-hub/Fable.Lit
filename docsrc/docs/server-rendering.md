---
title: Server rendering
layout: nacara-standard
---

Fable.Lit.Server renders Fable.Lit templates to HTML on .NET, with no JavaScript runtime involved. The same `html $"..."` view compiles two ways: by Fable against Fable.Lit, into lit templates in the browser, and by the .NET compiler against Fable.Lit.Server, into HTML on the server.

## Sharing a view

Both packages declare the `Lit` namespace, so a view file keeps `open Lit` and needs no conditional compilation. Put the views in a file that both projects compile: the Fable client references `Fable.Lit`, the server references `Fable.Lit.Server`, and neither references both.

```fsharp
// Shared/Views.fs, compiled by both projects
module Views

open Lit

type Item = { Name: string; Qty: int }

let row (item: Item) =
    html $"""<tr><td>{item.Name}</td><td>{item.Qty}</td></tr>"""

let table (items: Item list) =
    html $"""<table><tbody>{Lit.ofList (items |> List.map row)}</tbody></table>"""
```

On the server, render it to a string or to an [HtmlTypeProvider](https://www.nuget.org/packages/HtmlTypeProvider) `Node`:

```fsharp
open Lit

let markup: string = render (Views.table items)
let node = toNode (Views.table items) // composes into an HtmlTypeProvider page template's hole
```

Values are escaped. An event handler (`@click={Ev ...}`) and a property binding (`.prop={...}`) have no HTML form, so the server leaves them out. A boolean attribute (`?disabled={busy}`) is written when the value is `true` and left out when it is `false`.

A shared view can only use what both sides implement: `html`, `Ev`, `Lit.classes`, `Lit.ofList` and `Lit.nothing`. Anything the server can't honour, such as a directive that does its work inside lit, raises `UnsupportedTemplateValue` instead of writing an approximation.

## Hydration

`render` and `toNode` write plain HTML, which the browser then renders over. `renderHydratable` and `toHydratableNode` also write the marker comments lit's own SSR writes, so lit can adopt the markup in place instead of rebuilding it: the elements stay, and lit attaches its bindings to them.

In the browser, adopt it with `Hydrate.adopt`, passing the element the server rendered into and the same view with the same data. After that, `Lit.render` on the same container updates it as usual.

```fsharp
let container = document.getElementById "items"
Hydrate.adopt container (Views.table items)
// later
Lit.render container (Views.table newItems)
```

The markers are lit's internal protocol, and lit is strict about them: if the template in the browser differs from the one the server rendered, lit refuses to adopt. `Hydrate.adopt` then logs a warning to the console, clears the container and renders the view instead, so a mismatch costs a normal render rather than a broken page. It does the same when the container holds no markers at all. Different *data* is not caught. The markup is adopted as it is, still showing the server's values, while lit records the browser's as already rendered. Rendering the same data again changes nothing, so the server's values stay on the page until the data changes.

Pass the element the markers were written into. The server's root marker wraps whatever the view renders, so if the server wrote the view into `<div id="items">`, adopt that `div`, not an element inside it.

Markup lit is going to adopt can't contain a `<template>` element: lit counts it as one node and never looks inside, so every binding after it would be looked for on the wrong node. Fable.Lit.Server refuses to render one in hydratable markup.

### Elmish

`Program.withLitHydrated` mounts an Elmish program whose first render adopts the server's markup instead of replacing it. Every render after that is an ordinary one.

```fsharp
Program.mkProgram init update view
|> Program.withLitHydrated "app"
|> Program.run
```

The first model must be the one the server rendered from. A deterministic `init` does that by itself. An `init` that depends on server state has to be given the same state, for example by embedding it in the page. `Program.withLitHydratedOnElement` does the same for an element you already hold.

`Hydrate.fs` imports `@lit-labs/ssr-client`, and Fable.Lit.Elmish refers to it, so a bundler has to resolve that package for any app using either, even one that never hydrates. Fable.Lit declares it as an npm dependency for that reason. Nothing from it ends up in the bundle unless `Hydrate.adopt` is called.

`sample/Hydrate` in the repository is a complete example: an ASP.NET server renders the page with Fable.Lit.Server and the client adopts it.
