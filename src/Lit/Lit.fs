namespace Lit

open System
open Browser.Types
open Fable.Core

module Types =
    type RefValue<'T> =
        abstract value : 'T with get, set

    [<ImportMember("lit/directive.js")>]
    type Directive() =
        class end

    [<ImportMember("lit/async-directive.js")>]
    type AsyncDirective() =
        member _.isConnected: bool = jsNative
        member _.setValue(value: obj) : unit = jsNative

    type Part =
        interface end

    type ChildPart =
        inherit Part
        abstract parentNode : Element
        /// Tells everything rendered through this part whether it is still connected.
        /// AsyncDirectives inside it are notified, which is how they let go of timers,
        /// sockets and observers when the tree they live in leaves the page.
        abstract setConnected: connected: bool -> unit

    type ElementPart =
        inherit Part
        abstract element : Element

    // This type should come from Fable.Browser.Css but add it here
    // for now to avoid the dependency
    type CSSStyleSheet =
        abstract replace: css: string -> JS.Promise<unit>
        abstract replaceSync: css: string -> unit

open Types

/// The return type of the template tag functions.
type TemplateResult =
    interface end

/// The return type of the style tag functions.
type CSSResult =
    abstract cssText: string
    abstract styleSheet: CSSStyleSheet

type LitBindings =
    /// <summary>
    /// Interprets a template literal as an HTML template that can efficiently render to and update a container.
    /// </summary>
    [<ImportMember("lit")>]
    static member html: Template.JsTag<TemplateResult> = jsNative

    /// <summary>
    /// Interprets a template literal as an SVG template that can efficiently render to and update a container.
    /// </summary>
    [<ImportMember("lit")>]
    static member svg: Template.JsTag<TemplateResult> = jsNative

    [<ImportMember("lit")>]
    static member css: Template.JsTag<CSSResult> = jsNative

    /// <summary>
    /// Renders a value, usually a Lit TemplateResult, to the container.
    /// </summary>
    /// <param name="el">The container to render into.</param>
    /// <param name="t">A <see cref="Lit.TemplateResult">TemplateResult</see> to be rendered.</param>
    /// Returns lit's root part for the container. `Lit.render` ignores it; `Lit.renderPart`
    /// hands it back.
    [<ImportMember("lit")>]
    static member render(t: TemplateResult, el: Element) : ChildPart = jsNative

    /// <summary>
    /// A sentinel value that signals a ChildPart to fully clear its content.
    /// </summary>
    [<ImportMember("lit")>]
    static member nothing: TemplateResult = jsNative

    /// <summary>
    /// A sentinel value that signals a ChildPart to fully clear its content.
    /// </summary>
    [<ImportMember("lit")>]
    static member noChange: TemplateResult = jsNative

    /// <summary>
    /// Creates a user-facing directive function from a Directive class.
    /// This function has the same parameters as the directive's render() method.
    /// </summary>
    [<ImportMember("lit/directive.js")>]
    static member directive(cons: obj) : obj = jsNative

    /// <summary>
    /// A directive that applies CSS properties to an element.
    /// </summary>
    [<ImportMember("lit/directives/style-map.js")>]
    static member styleMap(styles: obj) : obj = jsNative

    /// <summary>
    /// A directive that applies dynamic CSS classes.
    /// </summary>
    [<ImportMember("lit/directives/class-map.js")>]
    static member classMap(classes: obj) : obj = jsNative

    /// <summary>
    /// Renders one of a series of values, including Promises, to a Part.
    /// </summary>
    [<ImportMember("lit/directives/until.js")>]
    static member until([<ParamArray>] values: obj []) : TemplateResult = jsNative

    /// <summary>
    /// A directive that repeats a series of values (usually TemplateResults) generated from an iterable,
    /// and updates those items efficiently when the iterable changes based on user-provided keys associated with each item.
    /// </summary>
    /// <remarks>
    /// Important: keys must be unique for all items in a given call to repeat.
    /// The behavior when two or more items have the same key is undefined.
    /// </remarks>
    /// <param name="items">An sequence of items to be repeated.</param>
    /// <param name="getId">A function that maps an item in the sequence to a unique string key.</param>
    /// <param name="template">A template that will be rendered for each item in the iterable.</param>
    [<ImportMember("lit/directives/repeat.js")>]
    static member repeat<'T>(items: 'T seq, getId: 'T -> string, template: 'T -> int -> TemplateResult) : TemplateResult = jsNative

    /// <summary>
    /// Enables fast switching between multiple templates by caching the DOM nodes and TemplateInstances produced by the templates.
    /// </summary>
    /// <param name="template">A template to be rendered.</param>
    [<ImportMember("lit/directives/cache.js")>]
    static member cache(template: TemplateResult) : TemplateResult = jsNative

    /// <summary>
    /// Prevents re-render of a template function until a single value or an array of values changes.
    /// </summary>
    /// <param name="deps">A set of dependencies that will be trigger a re-render when any of them changes.</param>
    /// <param name="render">A render function.</param>
    [<ImportMember("lit/directives/guard.js")>]
    static member guard(deps: obj array, render: unit -> TemplateResult) : TemplateResult = jsNative

    /// <summary>
    /// For AttributeParts, sets the attribute if the value is defined and removes the attribute if the value is undefined.
    /// </summary>
    /// <remarks>
    /// For other part types, this directive is a no-op.
    /// </remarks>
    /// <param name="value">A value to set the attribute to, or undefined to remove the attribute.</param>
    [<ImportMember("lit/directives/if-defined.js")>]
    static member ifDefined(value: obj) : TemplateResult = jsNative

    /// <summary>
    /// Sets the value of a Ref object or calls a ref callback with the element it's bound to.
    /// </summary>
    [<ImportMember("lit/directives/ref.js")>]
    static member ref(refOrCallback: obj) : TemplateResult = jsNative

    /// <summary>
    /// Creates a new Ref object, which is container for a reference to an element.
    /// </summary>
    [<ImportMember("lit/directives/ref.js")>]
    static member createRef<'T>() : RefValue<'T> = jsNative

    /// Renders the argument as HTML, rather than text.
    /// Note, this is unsafe to use with any user-provided input that hasn't been sanitized or escaped, as it may lead to cross-site-scripting vulnerabilities.
    [<ImportMember("lit/directives/unsafe-html.js")>]
    static member unsafeHTML(html: string) : TemplateResult = jsNative

/// The whole of the element this defines: two callbacks the browser already provides,
/// forwarded to whoever is listening. Private, because the thing worth exposing is
/// `Lit.trackConnection`, not a way to define elements.
module private ConnectionTracking =
    [<Emit("customElements.get($0)")>]
    let definedFor (tag: string) : obj = jsNative

    [<Emit("customElements.define($0, class extends HTMLElement { connectedCallback() { $1(this, true) } disconnectedCallback() { $1(this, false) } })")>]
    let define (tag: string, onChange: Element -> bool -> unit) : unit = jsNative

    [<Emit("$0['_$litPart$']")>]
    let rootPartOf (el: Element) : obj = jsNative

    /// One subscription: what to run when an element with the tag joins the document,
    /// and what that run left behind for when it leaves again. Kept per element, because
    /// a tag can have many and each has its own.
    type Subscription(onConnected: Element -> IDisposable) =
        let live = Collections.Generic.List<Element * IDisposable>()

        let indexOf (el: Element) =
            live.FindIndex(fun (candidate, _) -> obj.ReferenceEquals(candidate, el))

        member _.Changed(el: Element, connected: bool) =
            let existing = indexOf el

            if connected then
                // Guarded, because an element moved within the document reports its
                // arrival before its departure in some orders, and setting up twice would
                // leave the first one with nobody to dispose it.
                if existing < 0 then
                    live.Add(el, onConnected el)
            elif existing >= 0 then
                let _, disposable = live.[existing]
                live.RemoveAt existing
                disposable.Dispose()

        /// Stop listening, and dispose whatever is still set up.
        member _.Stop() =
            for _, disposable in Seq.toArray live do
                disposable.Dispose()

            live.Clear()

    /// Subscriptions by tag, so that subscribing twice does not define the element twice
    /// -- which the browser refuses anyway -- and so that one going away leaves the
    /// others, and the forwarding, alone.
    let private handlers = Collections.Generic.Dictionary<string, Collections.Generic.Dictionary<int, Subscription>>()
    let mutable private nextId = 0

    let private forTag (tag: string) =
        match handlers.TryGetValue tag with
        | true, existing -> existing
        | _ ->
            let created = Collections.Generic.Dictionary<int, Subscription>()
            handlers.[tag] <- created
            created

    /// The elements of each tag that are in the document right now.
    ///
    /// Kept because a handler can ask later than an element arrives, and on a
    /// server-rendered page always does: the element is in the markup, and defining its
    /// tag upgrades it on the spot, before whoever asked for the tag to be defined has
    /// been written down as listening. An arrival is only announced once, so anyone who
    /// was not listening at that moment has to be told from here.
    let private present = Collections.Generic.Dictionary<string, JS.Set<Element>>()

    let private presentFor (tag: string) =
        match present.TryGetValue tag with
        | true, existing -> existing
        | _ ->
            let created = JS.Constructors.Set.Create<Element>()
            present.[tag] <- created
            created

    let ensureDefined (tag: string) =
        if isNull (definedFor tag) then
            let subscribers = forTag tag
            let here = presentFor tag

            define (
                tag,
                fun el connected ->
                    if connected then
                        here.add el |> ignore
                    else
                        here.delete el |> ignore

                    // Nothing rendered into it yet is the ordinary case for the connected
                    // callback, which fires while the element is still being upgraded.
                    match rootPartOf el with
                    | null -> ()
                    | part -> (unbox<ChildPart> part).setConnected connected

                    for subscription in Seq.toArray subscribers.Values do
                        subscription.Changed(el, connected)
            )

    let subscribe (tag: string) (onConnected: Element -> IDisposable) =
        ensureDefined tag
        let subscribers = forTag tag
        let subscription = Subscription(onConnected)
        let id = nextId
        nextId <- nextId + 1
        subscribers.[id] <- subscription

        // Whatever is already in the document arrived before this was listening: the
        // elements a server rendered, upgraded a few lines up, and anything a tag that
        // was already tracked has gathered since. Their arrival is over, so it is told
        // again here. Telling it twice would be harmless -- a subscription sets up once
        // for an element it already holds -- but nothing else is going to tell it once.
        (presentFor tag).forEach (fun el _ _ -> subscription.Changed(el, true))

        { new IDisposable with
            member _.Dispose() =
                subscribers.Remove id |> ignore
                subscription.Stop() }

[<AutoOpen>]
module LitHelpers =
    /// <summary>
    /// Interprets a template literal as an HTML template that can efficiently render to and update a container.
    /// </summary>
    let html: Template.Tag<TemplateResult> = Template.transform LitBindings.html

    /// <summary>
    /// Interprets a template literal as an SVG template that can efficiently render to and update a container.
    /// svg is required for nested templates within an svg element
    /// </summary>
    let svg: Template.Tag<TemplateResult> = Template.transform LitBindings.svg

    /// CSS used in the Shadow DOM of LitElements
    let css: Template.Tag<CSSResult> = Template.transform LitBindings.css

    /// Just trims the braces {} out of a css block to be used in a `style` attribute
    let inline_css (css: string) =
        match css.IndexOf("{") with
        | -1 -> css
        | i ->
            match css.LastIndexOf("}") with
            | i2 when i2 > i -> css.[i+1..i2-1]
            | _ -> css

type Lit() =
    /// <summary>
    /// Interprets a template literal as an HTML template that can efficiently render to and update a container.
    /// </summary>
    static member html: Template.Tag<TemplateResult> = html

    /// <summary>
    /// Interprets a template literal as an SVG template that can efficiently render to and update a container.
    /// svg is required for nested templates within an svg element
    /// </summary>
    static member svg: Template.Tag<TemplateResult> = svg

    /// CSS used in the Shadow DOM of LitElements
    static member css: Template.Tag<CSSResult> = css

    /// <summary>
    /// Used when you don't want to render anything with Lit, usually in conditional expressions.
    /// </summary>
    static member nothing: TemplateResult = LitBindings.nothing

    /// <summary>
    /// Renders a Lit TemplateResult to the container.
    /// </summary>
    /// <param name="el">The container to render into.</param>
    /// <param name="t">A <see cref="Lit.TemplateResult">TemplateResult</see> to be rendered.</param>
    static member render el t: unit = LitBindings.render (t, el) |> ignore

    /// Renders into the container and hands back lit's root part for it.
    /// </summary>
    /// <remarks>
    /// The part is how a rendered tree is told it has been disconnected:
    /// <c>part.setConnected false</c> reaches every AsyncDirective inside it. Without it,
    /// markup rendered into a plain element has no lifecycle at all -- take the element
    /// out of the page and whatever it started keeps running, because nothing was ever
    /// told. Elements that are components have this already, through
    /// <c>Hook.useEffectOnce</c> and a LitElement's own disconnectedCallback.
    ///
    /// Calling it again on the same container returns the same part, so it is also how to
    /// get hold of the part for a container something else rendered into.
    /// </remarks>
    static member renderPart (el: Element) (t: TemplateResult) : ChildPart = LitBindings.render (t, el)

    /// <summary>
    /// Makes every element with this tag report leaving and rejoining the document to
    /// whatever lit rendered into it.
    /// </summary>
    /// <remarks>
    /// lit propagates disconnection through its own rendering: clear a part, and the
    /// directives in what it removed are told. What it never does is watch the document,
    /// so markup rendered into an element that something else removes hears nothing.
    /// LitElement does not solve that either -- it borrows the only mechanism there is,
    /// its own connected and disconnected callbacks, and calls setConnected from them.
    ///
    /// This borrows the same one. The tag becomes a custom element whose only behaviour
    /// is to forward those callbacks to the root part inside it, so AsyncDirectives pause
    /// when the host leaves the document and resume when it returns. It pauses rather than
    /// stops on purpose: an element that was merely moved comes back with everything it
    /// had.
    ///
    /// Call it once. Defining a tag upgrades the instances already on the page, so it can
    /// run before or after the markup arrives. The tag must contain a dash, as custom
    /// element names must.
    /// </remarks>
    /// <example>
    ///     Lit.trackConnection "my-island"
    /// </example>
    static member trackConnection(tag: string) : unit = ConnectionTracking.ensureDefined tag

    /// <summary>
    /// The same, and runs something of yours while the element is in the document.
    /// </summary>
    /// <remarks>
    /// The handler is called when an element with this tag joins the document, and what
    /// it returns is disposed when that element leaves it again -- so a timer, a socket
    /// or a listener is set up and torn down in one place, and neither half can be
    /// written without the other. Rejoining calls it again, because it left.
    ///
    /// The forwarding to the rendered tree still happens; this is in addition to it.
    ///
    /// The first call arrives while the element is being upgraded, before anything has
    /// been rendered into it, so a handler that expects content has to allow for that.
    ///
    /// An element that is already in the document when this is called has arrived as
    /// far as the handler is concerned, and is reported straight away. That is every
    /// element a server rendered, and every element of a tag that was already tracked.
    /// </remarks>
    /// <example>
    ///     Lit.trackConnection("my-island", fun el ->
    ///         let id = JS.setInterval (fun () -> tick el) 1000
    ///         Hook.createDisposable (fun () -> JS.clearInterval id))
    /// </example>
    /// <returns>
    /// A disposable that stops listening and disposes whatever is still set up. It does
    /// not undefine the element, which the platform does not allow and which nothing else
    /// needs: the forwarding carries on, and so do any other subscriptions. Shaped this
    /// way to sit in a hook, where <c>Hook.useEffectOnce</c> wants something to dispose.
    /// </returns>
    static member trackConnection(tag: string, onConnected: Element -> IDisposable) : IDisposable =
        ConnectionTracking.subscribe tag onConnected

    /// <summary>
    /// Generates a single string that filters out false-y values from a tuple sequence.
    /// </summary>
    static member classes(classes: (string * bool) seq): string =
        classes
        |> Seq.choose (fun (s, b) -> if b then Some s else None)
        |> String.concat " "

    /// <summary>
    /// Generates a string from the string sequence provided
    /// </summary>
    static member classes(classes: string seq): string = classes |> String.concat " "

    /// <summary>
    /// Generates inline styles in an efficient way for the browser to apply
    /// </summary>
    /// <example>
    /// <code lang="fsharp">
    ///    let styles = {| backgroundColor = "rebeccapurple"; color = "white" |}
    ///    html $"&lt;div style={Lit.styles styles}&gt;This is my Content&gt;/div&gt;
    /// </code>
    /// </example>
    static member styles(styles: obj) = LitBindings.styleMap styles

    /// <summary>
    /// Generates inline styles in an efficient way for the browser to apply
    /// </summary>
    /// <example>
    /// <code lang="fsharp">
    ///    let styles = [ "backgroundColor","rebeccapurple"; "color", "white" ]
    ///    html $"&lt;div style={Lit.styles styles}&gt;This is my Content&gt;/div&gt;
    /// </code>
    /// </example>
    static member styles(styles: (string * obj) seq) =
        JsInterop.createObj styles |> Lit.styles
        
    /// <summary>
    /// Generates inline styles in an efficient way for the browser to apply
    /// </summary>
    /// <example>
    /// <code lang="fsharp">
    ///    let styles = dict ([ "backgroundColor","rebeccapurple"; "color", "white" ])
    ///    html $"&lt;div style={Lit.styles styles}&gt;This is my Content&gt;/div&gt;
    /// </code>
    /// </example>
    static member styles(styles: Map<string, obj>) =
        styles |> Map.toSeq |> LitBindings.styleMap

    /// <summary>
    /// Give a unique id to items in a list. This can improve performance in lists that will be sorted, filtered or re-ordered.
    /// </summary>
    /// <param name="getId">A function that maps an item in the sequence to a unique string key.</param>
    /// <param name="template">A rendering function based on the items of the sequence.</param>
    /// <param name="items">A sequence of items to be rendered.</param>
    static member mapUnique (getId: 'T -> string) (template: 'T -> TemplateResult) (items: 'T seq): TemplateResult =
        LitBindings.repeat (items, getId, (fun x _ -> template x))
    
    /// <summary>
    /// Give a unique id to items in a list and retrieve the current index . This can improve performance in lists that will be sorted, filtered or re-ordered.
    /// </summary>
    /// <param name="getId">A function that maps an item in the sequence to a unique string key.</param>
    /// <param name="template">A rendering function based on the items of the sequence that also supplies the current index.</param>
    /// <param name="items">A sequence of items to be rendered.</param>
    static member mapiUnique (getId: 'T -> string) (template: int -> 'T -> TemplateResult) (items: 'T seq): TemplateResult =
        LitBindings.repeat (items, getId, (fun x i -> template i x))

    /// Shows the placeholder until the promise is resolved
    static member ofPromise(template: JS.Promise<TemplateResult>, ?placeholder: TemplateResult): TemplateResult =
        LitBindings.until(template, defaultArg placeholder Lit.nothing)

    /// <summary>
    /// Lazily import a register or render function from another module, this helps JS bundlers to split the code and optimize loading times.
    /// Be careful not to reference anything else from the imported module.
    /// </summary>
    /// <example>
    ///     Lit.ofImport(MyComponent, fun render -> render "foo" "bar")
    /// </example>
    /// <example>
    ///     Lit.ofImport(MyWebComponent.register, fun _ -> html $"<my-web-component></my-web-component>")
    /// </example>
    static member inline ofImport(registerOrRenderFunction: 'Fn, template: 'Fn -> TemplateResult, ?placeholder: TemplateResult): TemplateResult =
        LitBindings.until((JsInterop.importValueDynamic registerOrRenderFunction).``then``(fun fn -> template fn), defaultArg placeholder Lit.nothing)

    /// Only re-render the template if one of the dependencies changes.
    static member onChange(dependency: 'T, template: 'T -> TemplateResult): TemplateResult =
        let dependencies = if JS.Constructors.Array.isArray dependency then unbox dependency else [| box dependency |]
        LitBindings.guard(dependencies, fun () -> template dependency)

    /// Only re-render the template if one of the dependencies changes.
    static member onChange(dependency1: 'T1, dependency2: 'T2, template: 'T1 -> 'T2 -> TemplateResult): TemplateResult =
        LitBindings.guard([|dependency1; dependency2|], fun () -> template dependency1 dependency2)

    /// Only re-render the template if one of the dependencies changes.
    static member onChange(dependency1: 'T1, dependency2: 'T2, dependency3: 'T3, template: 'T1 -> 'T2 -> 'T3 -> TemplateResult): TemplateResult =
        LitBindings.guard([|dependency1; dependency2; dependency3|], fun () -> template dependency1 dependency2 dependency3)

    /// Only re-render the template if one of the dependencies changes.
    static member onChange(dependency1: 'T1, dependency2: 'T2, dependency3: 'T3, dependency4: 'T4, template: 'T1 -> 'T2 -> 'T3 -> 'T4 -> TemplateResult): TemplateResult =
        LitBindings.guard([|dependency1; dependency2; dependency3; dependency4|], fun () -> template dependency1 dependency2 dependency3 dependency4)

    /// To respect list item identities when sorting, inserting, removing... use `mapUnique`
    static member inline ofSeq(items: TemplateResult seq) : TemplateResult = unbox items

    /// To respect list item identities when sorting, inserting, removing... use `mapUnique`
    static member inline ofList(items: TemplateResult list) : TemplateResult = unbox items

    /// To respect list item identities when sorting, inserting, removing... use `mapUnique`
    static member inline ofArray(items: TemplateResult array) : TemplateResult = unbox items

    static member inline ofStr(v: string) : TemplateResult = unbox v

    static member inline ofText(v: string) : TemplateResult = unbox v

    static member inline ofInt(v: int) : TemplateResult = unbox v

    static member inline ofFloat(v: float) : TemplateResult = unbox v

    /// <summary>
    /// Sets the attribute if the value is Some and removes the attribute if the value is None.
    /// </summary>
    /// <remarks>
    /// ONLY ATTRIBUTES.
    /// </remarks>
    /// <param name="attributeValue">A value to set the attribute to</param>
    static member ifSome(attributeValue: string option) = LitBindings.ifDefined attributeValue

    /// <summary>
    /// When placed on an element in the template, the ref directive will retrieve a reference to that element once rendered.
    /// </summary>
    /// <example>
    ///     &lt;input {Lit.refValue inputRef}&gt;
    /// </example>
    static member refValue<'El when 'El :> Element>(r: ref<'El option>): TemplateResult =
        LitBindings.ref
            { new RefValue<'El option> with
                member _.value with get() = r.Value
                                and set(v) = r.Value <- v }

    /// <summary>
    /// When placed on an element in the template, the callback will be called each time the referenced element changes.
    /// If a ref callback is rendered to a different element position or is removed in a subsequent render,
    /// it will first be called with undefined, followed by another call with the new element it was rendered to (if any).
    /// </summary>
    /// <example>
    ///     &lt;input {Lit.refCallback inputFn}&gt;
    /// </example>
    static member refCallback<'El when 'El :> Element>(fn: 'El option -> unit): TemplateResult = LitBindings.ref fn

    /// Used when building custom directives. [More info](https://lit.dev/docs/templates/custom-directives/).
    static member inline directive<'Class, 'Arg>() : 'Arg -> TemplateResult =
        LitBindings.directive JsInterop.jsConstructor<'Class> :?> _

[<AutoOpen>]
module DomHelpers =
    type ShadowRoot with
        [<Emit("$0.adoptedStyleSheets")>]
        member _.adoptedStyleSheets: CSSStyleSheet[] = jsNative

    type EventTarget with
        /// Casts the event target to HTMLInputElement and gets the `value` property.
        member this.Value: string = (this :?> HTMLInputElement).value

        /// Casts the event target to HTMLInputElement and gets the `checked` property.
        member this.Checked: bool = (this :?> HTMLInputElement).``checked``

    /// Wrapper for event handlers to help type checking.
    let inline Ev (handler: #Event -> unit): #Event -> unit = handler

    /// Wrapper for event handlers to help type checking.
    /// Extracts `event.target.value` and passes it to the handler.
    let inline EvVal (handler: string -> unit): Event -> unit =
        fun (ev: Event) -> handler ev.target.Value
