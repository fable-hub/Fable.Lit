---
title: Hook Components
layout: nacara-standard
---

Fable.Lit includes the `HookComponent` attribute. When you decorate a view function with it, this lets you use [hooks](https://react.dev/reference/react/hooks) in a similar way as [ReactComponent](https://zaid-ajaj.github.io/Feliz/#/Feliz/React/NotJustFunctions) attribute does. Hook support is included in Fable.Lit's F# code and doesn't require any extra JS dependency besides Lit.

```fsharp
open Lit

[<HookComponent>]
let NameInput() =
    let value, setValue = Hook.useState "World"
    let inputRef = Hook.useRef<HTMLInputElement>()

    html $"""
      <div class="content">
        <p>Hello {value}!</p>
        <input {Lit.refValue inputRef}
          value={value}
          @keyup={EvVal setValue}
          @focus={Ev(fun _ -> inputRef.Value |> Option.iter (fun el -> el.select()))}>
      </div>
    """
```

:::info
Note that HookComponents are just a way to keep state between renders and doesn't create a custom HTML element. Check [Web Components](./web-components.html) if you want to declare a component that can be instantiated from HTML.
:::

Fable.Lit hooks in general have the same API as their React counterparts but may differ in some occasions:

- `useState`
- `useMemo`
- `useRef`: Can use "native" F# refs.
- `useEffect`: Doesn't accept a dependency array, instead it provides semantic alternatives for each use case.
    - `useEffect`: Trigger an effect after each render.
    - `useEffectOnce`: Trigger an effect only once after the first render.
    - `useEffectOnChange`: Trigger an effect after each render **if** the given value has changed.

## UseElmish

Thanks to the great work by [Cody Johnson](https://twitter.com/Cody_S_Johnson) with [Feliz.UseElmish](https://zaid-ajaj.github.io/Feliz/#/Hooks/UseElmish), Fable.Lit HookComponents also include `useElmish` hook to manage the internal state of your components using the model-view-update architecture.

```fsharp
open Elmish
open Lit

type Model = ..
type Msg = ..

let init() = ..
let update msg model = ..
let view model dispatch = ..

[<HookComponent>]
let Clock(): TemplateResult =
    let model, dispatch = Hook.useElmish(init, update)
    view model dispatch
```

## Scoped CSS

Scoped CSS is a technique used by CSS modules or frameworks like Vue or Svelte to provide style encapsulation even when using global CSS. The trick is to automatically prefix all your CSS selectors with a class name that will be used in the root of your component. This way you make sure the rules won't affect anything outside your component. Since 1.4, Fable.Lit provides the `use_scoped_css` hook to do this: it will automatically generate a unique id as the class name and automatically prefix all the selectors in your CSS. Then it returns the class name so you can use it in your Lit template.

::info
When a selector starts with `:host`, this is replaced by the class name instead of prefixing it. @keyframes names will also be prefixed too so you can make sure they won't conflict with other styles.

```fsharp
[<HookComponent>]
let ClockDisplay model dispatch =
    Hook.useHmr (hmr)
    let transitionMs = 800
    let clasName = Hook.use_scoped_css $"""
        .clock-container {{
            transition-duration: {transitionMs}ms;
        }}
        .clock-container.transition-enter {{
            opacity: 0;
            transform: scale(2) rotate(1turn);
        }}
        .clock-container.transition-leave {{
            opacity: 0;
            transform: scale(0.1) rotate(-1.5turn);
        }}

        @keyframes move-side-by-side {{
            from {{ margin-left: -50%%; }}
            to {{ margin-left: 50%%; }}
        }}
        button {{
            animation: 1.5s linear 1s infinite alternate move-side-by-side;
        }}
        """

    let transition =
        Hook.useTransition(
            transitionMs,
            onEntered = (fun () -> ToggleClock true |> dispatch),
            onLeft = (fun () -> ToggleClock false |> dispatch))

    let clockContainer() =
        html $"""
            <div class="clock-container {transition.className}">
                <my-clock
                    minute-colors="white, red, yellow, purple"
                    hour-color="yellow"></my-clock>
            </div>
        """

    html $"""
        <div class="{clasName} vertical-container">

            <button class="button"
                style="margin: 1rem 0"
                ?disabled={transition.isRunning}
                @click={Ev(fun _ ->
                    if model.ShowClock then transition.triggerLeave()
                    else transition.triggerEnter())}>
                {if model.ShowClock then "Hide" else "Show"} clock
            </button>

            {if transition.hasLeft then Lit.nothing else clockContainer()}
        </div>
    """
```

> `Hook.use_scoped_css` uses snake case for compatibility with the [F# templates VS Code extension](https://marketplace.visualstudio.com/items?itemName=alfonsogarciacaro.vscode-template-fsharp-highlight).

Scoped CSS is also compatible with [web components](./web-components.html) when you are not using Shadow DOM.

## Writing your own hook

The magic behind Fable.Lit's hooks is the context is provided by the JS `this` keyword. To access the context from the render function you must use an `inline` helper and then pass the context to your custom hook. Usually we implement the inlined helper as an static extension of `Hook` type, and the hook itself as an extension of `Lit.HookContext`. Example:

```fsharp
module MyHooks =
    open Lit

    // Updating a ref doesn't cause a re-render,
    // so let's use a ref with the same signature as useState
    type HookContext with
        member ctx.useSilentState(v: 'Value) =
            let r = ctx.useRef(v)
            r.Value, fun v -> r := v

    type Hook with
        // IMPORTANT! This function must be inlined to access the context from the render function
        static member inline useSilentState(v: 'Value): 'Value * ('Value -> unit) =
            Hook.getContext().useSilentState(v)
```
