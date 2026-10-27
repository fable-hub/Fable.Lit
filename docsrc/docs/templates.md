---
title: Templates
layout: nacara-standard
toc:
    to: 3
---

Lit allows you to render web UIs in a declarative way as [React](https://reactjs.org/) does by using HTML templates. With Fable.Lit you can use [F# interpolated strings](https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/interpolated-strings) to build the HTML templates. Also, if you're using Visual Studio Code, we recommend the [Highlight F# Templates](https://marketplace.visualstudio.com/items?itemName=alfonsogarciacaro.vscode-template-fsharp-highlight) extension for IDE support of HTML and CSS as embedded languages in F#.

If you check [Lit's website](https://lit.dev) you will likely think it focus on [Web Components](https://developer.mozilla.org/en-US/docs/Web/Web_Components) (which [are also part of](./web-components.html) Fable.Lit), but HTML templates by themselves are already a very powerful feature and it's entirely possible to build your web app with them, or even use "virtual" [React-like components](./hook-components.html).

You can use the holes in the interpolated string to add dynamic data to the HTML templates. Normally the holes are only allowed in the attribute value or child node position. When you open the `Lit` namespace, the `html` and `svg` function will be available to declare the templates:

```fsharp
open Lit

[<HookComponent>]
let MyComponent() =
    Hook.useHmr(hmr)
    let value, setValue = Hook.useState "World"

    html $"""
      <div class="content">
        <p>Local state: <i>Hello {value}!</i></p>
        <input
          value={value}
          @keyup={EvVal setValue}>
      </div>
    """
```

:::info
Given that holes in interpolated strings are not typed, is a good idea to wrap your event handler with `Ev` to make sure you're using the correct type. `EvVal` is also available to access `ev.target.value` string in common input's `change` events.
:::

Use `Lit.render` to mount the template in a DOM element. If the template was already mounted, it will be updated.

```fsharp
open Browser

let el = document.getElementById("my-container")
MyComponent() |> Lit.render el
```

<br />

Lit uses standard HTML for the templates, with only three special characters `@`/`?`/`.` in some situations:

<table>
    <thead>
        <tr>
            <th>Type</th>
            <th>Example</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td>Event listeners</td>
            <td>

```html
<button @click={Ev(fun ev -> doSomething())}>Click me!</button>
```

</td>
        </tr>
        <tr>
            <td>Boolean attributes</td>
            <td>

```html
<button ?disabled={not enabled}>Click me!</button>
```

</td>
        </tr>
        <tr>
            <td>Properties</td>
            <td>

```html
<my-component .someData={nonStringData}></my-component>
```

</td>
        </tr>
    </tbody>
</table>

:::info
You rarely need to pass a [property instead of an attribute](https://stackoverflow.com/a/6004028) unless you're using a Web Component that asks you to do so.
:::

If you want to learn more about templates, please check [Lit's website](https://lit.dev/docs/templates/overview/).

## Directives

Lit includes special functions, called "directives" that can control the way the templates are rendered. The `Lit` class provides the following directives and helpers as static members:

:::info
The name and signature of some directives have been adapted from Lit to be more idiomatic in F#.
:::

### nothing

Used when you don't want to render anything with Lit, usually in conditional expressions.

```fsharp
html $"""Value: { if value > 0 then value else Lit.nothing } """
```

### classes

Generates a single string from a sequence of classes, or `string * bool` tuples (false values will be filtered out).

```fsharp
let classes = Lit.classes [
    "button", true
    "is-active", isActive
]

html $"""<button class={classes}>Click me</button>"""
```

### mapUnique

You can pass any iterable (including F# lists) to Lit, but when it's important to identify items in a list (e.g. when the list can be sorted, filtered or included item transitions), use `Lit.mapUnique` to give each item a unique id.

```fsharp
let getItemTemplate item =
    html $"""<li>{item.name} - {item.price}</li>"""

html $"""
    <ul>
        {items |> Lit.mapUnique
            (fun item -> item.id)
            getItemTemplate}
    </ul>
    """
```

### ifSome

Sets the attribute if the value is Some and removes the attribute if the value is None.

```fsharp
html $"""<img src="/images/${ifSome filename}">"""
```

### onChange

Prevents re-render of a template function unless one of the dependencies has changed.

```fsharp
let greetingTemplate (name: string) (age: int) =
    html $"Hey {name}! You are {age} years old!"

Lit.onChange("Angel", 10, greetingTemplate)
```

### ofPromise

Shows the placeholder until the promise is resolved.

```fsharp
let deferredTemplate = promise {
    do! Promise.sleep 80
    return html $"<p>Sorry for being late!</p>"
}

Lit.ofPromise(deferredTemplate, placeholder=html $"<p>I'm already here!</p>")
```

### ofImport

Lazily imports a register or render function from another module, this helps JS bundlers to split the code and optimize loading times. Be careful not to reference anything else from the imported module.

```fsharp
// Components.fs

module Components

[<HookComponent>]
let MyComponent(text: string) = // ..

// App.fs

Lit.ofImport(Components.MyComponent, fun render -> render "something")
```

:::info
You can access to the raw bindings via the `LitBindings` static class.
Check the full list in the [lit docs](https://lit.dev/docs/api/directives/)
:::

## Elmish

Fable.Lit.Elmish allows you to write a frontend app using the popular [Elmish](https://elmish.github.io/) library by [Eugene Tolmachev](https://github.com/et1975) with a view function returning `Lit.TemplateResult`. For hot reloading, see [Hot Reloading](./hmr.html).

```fsharp
open Elmish
open Lit

type Model = ..
type Msg = ..

let init() = ..
let update msg model = ..
let view model dispatch = ..

open Lit.Elmish

Program.mkProgram initialState update view
|> Program.withLit "app-container"
|> Program.run
```
