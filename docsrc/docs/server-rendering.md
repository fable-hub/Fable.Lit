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

In the browser, adopt it with `hydrate` from `@lit-labs/ssr-client`, passing the same view with the same data the server rendered. After that, `Lit.render` on the same container updates it as usual.

```fsharp
[<Import("hydrate", "@lit-labs/ssr-client")>]
let hydrate (template: TemplateResult) (container: Browser.Types.Element) : unit = jsNative

let container = document.getElementById "items"
hydrate (Views.table items) container
// later
Lit.render container (Views.table newItems)
```

The markers are lit's internal protocol, and lit is strict about them: if the template or the data in the browser differs from what the server rendered, `hydrate` throws. For the same reason, markup lit is going to adopt can't contain a `<template>` element: lit counts it as one node and never looks inside, so every binding after it would be looked for on the wrong node.
