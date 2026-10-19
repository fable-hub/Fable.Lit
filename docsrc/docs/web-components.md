---
title: Web Components
layout: nacara-standard
---

[web components]: https://developer.mozilla.org/en-US/docs/Web/Web_Components
[fast]: https://fast.design
[spectrum web components]: https://github.com/adobe/spectrum-web-components
[vaadin]: https://vaadin.com/components
[fluent ui]: https://github.com/microsoft/fluentui/tree/master/packages/web-components
[Lit mechanisms to declare reactive properties]: https://lit.dev/docs/components/properties/
[ionic framework]: https://ionicframework.com/docs/components
[hooks]: ./hook-components.html
[testing]: ./testing.html
[custom css properties]: https://developer.mozilla.org/en-US/docs/Web/CSS/Using_CSS_custom_properties
[css parts]: https://developer.mozilla.org/en-US/docs/Web/CSS/::part
[shadow dom]: https://developer.mozilla.org/en-US/docs/Web/Web_Components/Using_shadow_DOM

[Web Components] are a way to create custom reusable HTML Elements and are part of the browser standards. These elements work as any other HTML tag like a `div`, `span`, or `article` meaning that you can produce web components that are able to be used by any framework without having to write any kind of wrapper on top of it.

:::info
These web components use [Lit](https://lit.dev) under the hood, visit [Lit.dev](https://lit.dev) for more detailed information on how `LitElement` Web Components work.
:::

Let's see a quick simple example of a web component:

```fsharp
[<LitElement("my-counter")>]
let Counter() =
    // This call is obligatory to initialize the web component
    let _, props =
        LitElement.init(fun init ->
            init.props <- {| initial = Prop.Of(defaultValue = 0) |})

    let counter, setCounter = Hook.useState props.initial.Value

    html
        $"""
        <article>
            <p>{counter}</p>
            <button @click={fun _ -> setCounter(counter + 1)}>+</button>
            <button @click={fun _ -> setCounter(counter - 1)}>-</button>
        </article>
        """
```

<br/>

And then in HTML you can use it as:

```html
<my-counter initial="10"></my-counter>
```

<br/>

> When we say you can use it in HTML we mean it, wherever you are used to put an HTML tag you are able to use it, vue, angular, server side pages... You name it.

## Registering

Web components are automatically registered when the function decorated by `[<LitElement>]` is loaded. However, since the function is not called directly it may happen the file where the function is declared doesn't get any reference from the entry point of your application. In those cases, you can include a "dummy" register function only for the purpose of referencing the file containing the component from the entry point of your application.

```fsharp
// MyComponent.fs

let register() = ()

[<LitElement("my-component")>]
let MyComponent() = ...


// App.fs

MyComponent.register()

html $"<my-component> </my-component>"
```

## Properties and attributes

As we've just seen, Web Components must be initialized in HTML, we cannot call the function directly. Because of this, arguments must be passed through HTML properties/attributes. In DOM elements, the attributes is what you see in the HTML as in `<input type="text">`, while properties are getters/setters as in any other JS object: `myInput.value = "foo"`. Attributes only accept string, while properties accept any value.

In Fable.Lit you declare the component's custom properties when initializing and by default Lit will create an attribute with the same name. For non-string properties you can pass a converter to decode the value from the attribute string (numbers and booleans will be converted automatically). Also, you can set a custom attribute name with the `attribute` parameter or just disable the attribute if you pass an empty string.

Whenever one of the properties change Lit will trigger a new render of the component.

```fsharp
LitElement.init(fun config ->

    let split (str: string) =
        str.Split(',') |> Array.map (fun x -> x.Trim()) |> Array.toList

    config.props <-
        {|
            // "selected" attribute is created
            selected = Prop.Of("lightgreen")

            // "colors" attribute is create and split function is used to convert the string value
            colorList = Prop.Of([], attribute="colors", fromAttribute = split)

            // This property won't be exposed as attribute
            onlyProp = Prop.Of({| foo = 5 |}, attribute="")
        |}
)
```

<br/>

Remember that Lit templates allow you to pass values to properties using a dot. Like this you can pass values that are not strings.

```fsharp
html $"<my-element .onlyProperty={ {| foo = 4 |} }></my-element>"
```

## Events

Because a LitElement becomes just another HTML Element, you can use it to trigger HTML Events. Same as with attributes, events can be used to make your component communicate with the external world in a standard way regardless of the framework or even the language.

```fsharp
[<LitElement("product-settings")>]
let CloseMe() =
    let host, props =
        LitElement.init(fun init ->
            init.props <- {| product = Prop.Of({ (* ... product definition ...*) }) |})

    let product = props.product.Value

    let onDeleteFromInventory _ =
        host.dispatchEvent("on-delete-from-inventory")

    let onHideFromCustomers _ =
        host.dispatchCustomEvent("on-hide-from-customers", { product with hidden = true })

    html
        $"""
        <article>
            <header>
                <h3>{product.name}</h3>
            </header>
            <aside>
                <button @click={onHideFromCustomers}>Delete</button>
                <button @click={onDeleteFromInventory}>Hide</button>
            </aside>
            <section>
                <!-- the rest of the component -->
            </section>
        </article>
        """
```

`host.dispatchEvent` and `host.dispatchCustomEvent` have the following optional arguments

- detail -> (CustomEvent only) A value or object that you want to send to the listeners of this event
- bubbles -> Allow the event to enter the bubbling phase
- composed -> Let the event cross the shadow DOM boundary
- cancelable -> Allow this event to be default prevented

:::info
These parameters are set to true by default (except from detail) for convenience since most of the time this is what you want when you dispatch events
:::

### Typed custom events

With `dispatchCustomEvent`, the sender and each listener spell out the event name, and every listener casts `detail` to the type it expects. If the sender's payload changes, the listeners still compile and read the wrong shape at runtime.

`customEvent<'T> "name"` declares the name and the type of `detail` in one value. Raise it with `dispatchCustom` on any `EventTarget` (`document` included, so components that hold no reference to each other can still talk), and listen with `Hook.useEventListener`, which hands the handler the payload already typed:

```fsharp
let itemAdded = customEvent<string * int> "item-added"

// Sender
document.dispatchCustom(itemAdded, ("apples", 3))

// Listener, in a HookComponent or LitElement
Hook.useEventListener(document, itemAdded, fun (name, count) ->
    setLast $"{count} {name}")
```

Change the payload type in `itemAdded` and both ends stop compiling until they agree again. `dispatchCustom` takes the same optional `bubbles`, `composed` and `cancelable` arguments as `dispatchCustomEvent`, also `true` by default.

## Styles

By default, Lit Elements will attach a [Shadow DOM] to the generated HTML Element. The main advantage of the Shadow DOM is **style encapsulation**, meaning the infamous CSS conflict's won't happen between your component and the rest of the document. On the other hand, this also means global styles won't affect your component, or you cannot use CSS frameworks like Bootstrap or Bulma.

To define styles for your Lit Elements with a shadow DOM, provide them in the init function.

```fsharp
[<LitElement("my-element")>]
let MyElement()
    let _ = LitElement.init(fun init ->
        init.styles <- [
            yield! Shared.styles

            css $"""
            :host {{
                display: flex;
                flex-direction: column;
            }}

            p {{
                color: red;
            }}
            """
        ])
    html
        $"""
        <p>This text is red!</p>
        <my-other-element header="This element's p's won't be red"></my-other-element>
        """
```

:::info
Note you can mix custom styles with base styles shared with other components (`Shared.styles` in the example). `Lit.css` function requires a template string so remember to escape braces `{{` `}}` as required by F# interpolation syntax.
:::

Assuming `<my-other-element header="This element's p's won't be red"></my-other-element>` is another web component (meaning it has it's own Shadow DOM) the `p` tags inside that element won't be affected by our own CSS styles.

Even if the shadow DOM provides CSS Encapsulation, customization is still possible via:

- [Custom CSS Properties]
- [Css Parts]

To know more about applying styles to LitElement check the [Lit docs on theming](https://lit.dev/docs/components/styles/#theming).

:::info
Shadow DOM is supported by modern browsers and can be [polyfilled for legacy browsers](https://lit.dev/docs/tools/requirements/#loading-polyfills).
:::

### Web Components without Shadow DOM

Custom Elements and Shadow DOM are actually different technologies, so it's possible to have a `LitElement` without a shadow DOM if you want to use global styles or a CSS framework. In this case, you can use [scoped CSS](./hook-components.html#scoped-css) instead:

```fsharp
[<LitElement("my-element")>]
let MyElement()
    let _ = LitElement.init(fun config ->
        config.useShadowDom <- false
    )
    let className =
        Hook.use_scoped_css """
            p {
                color: red;
            }
        """
    html $"""
    <div class={className}>
        <p>This text is still red!</p>
    </div>
    """
```

## Differences with Hook Components

- Web Components are actual HTML elements that live in the DOM. Hook Components are just abstractions based on [Lit async directives](https://lit.dev/docs/templates/custom-directives/#async-directives)
- Instantiate Web components in HTML, don't call the function directly
- Arguments must be passed through HTML attributes/properties
- Lit also has more fine-grained control of a LitElement lifecycle, sometimes this is needed for example when using the [animate directive](https://www.npmjs.com/package/@lit-labs/motion)

:::info
Fable.Lit hooks and HMR are compatible both with Hook and Web Components
:::

## Use cases

Popular libraries made with Web components are

- [FAST] - Microsoft
  - [Fluent UI] - The design langauge that powers Windows
- [Spectrum Web Components] - Adobe Spectrum design language
- [Vaadin]
- [Ionic Framework]

> Most of those are actually built with Lit as well!

If these libraries already provide UI libraries why would you want to author web components?

- Testability
- Design Systems/Branding
- Portability

#### Testability

Components are a single unit of the UI they have behavior and state it's easier to test them in isolation.
They are similar to functions

- `inputs -> processing -> outputs`
- `attr/props -> processing | User Interaction -> UIChanges | Events`

That means that it should be easy to determine which things changed inside a component when their properties or attributes have changed, if the user changed something or if the element is trying to communicate with the parent elements

:::info
Check [Testing](./testing.html) to know more about how can you test Lit components
:::

#### Design Systems

If you're part of a company with multiple teams, chances are that those teams might have chosen different tools to work with if your company wants to implement a branding or a uniform way to distinguish itself from others it would be hard to create components in each library, web components are a perfect fit for that, Adobe Spectrum and Ionic Framework are excelent examples of that they define dozens of web components with a particular design language that makes the brand easily recognizable.

#### Portability

Web components are designed to work in the browser, once you include the script tag and register them you can use them in any UI library Angular, Vue, Server, etc. write once use everywhere.
