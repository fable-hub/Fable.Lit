namespace Lit

open System
open Fable.Core
open Fable.Core.JsInterop
open Fable.React
open Browser.Types
open Lit

/// A React root, and the function that makes one.
///
/// Declared here because Fable.React binds only the old `react-dom` entry point, not
/// `react-dom/client`. It is the whole of the `react-dom/client` surface this file
/// needs.
type IReactRoot =
    abstract render: element: ReactElement -> unit
    abstract unmount: unit -> unit

[<AutoOpen>]
module private ReactDomClientBindings =
    [<Import("createRoot", "react-dom/client")>]
    let createRoot (container: Element) : IReactRoot = jsNative

/// <summary>
/// Directive that allows a react component to be rendered inside a Lit template.
/// </summary>
[<AttachMembers>]
type ReactDirective() =
    inherit Types.AsyncDirective()

    let mutable _domEl = Unchecked.defaultof<Element>

    // The root React renders through, and the element it was created for.
    let mutable _root = Unchecked.defaultof<IReactRoot>

    member _.className = ""
    member _.renderFn = Unchecked.defaultof<obj -> ReactElement>

    member this.render(props: obj) =
        Lit.html $"""<div class={this.className} {Lit.refCallback(function
            | Some el when this.isConnected ->
                // createRoot, not ReactDom.render: the latter was deprecated in React 18
                // and is gone in 19, where this directive raised rather than rendered.
                //
                // One root per element, and only while it is the element being rendered
                // into. Keeping the first root forever would render into a container lit
                // had already replaced; making a new one per render would mount the
                // component again every time, discarding whatever state it held.
                if isNull (box _root) || not (obj.ReferenceEquals(_domEl, el)) then
                    if not (isNull (box _root)) then
                        _root.unmount()

                    _root <- createRoot el

                _domEl <- el
                _root.render(this.renderFn props)
            | _ -> ()
        )}></div>"""

    member _.disconnected() =
        // unmount on the root, for the same reason. unmountComponentAtNode belongs to
        // the API that created the root the old way, and does nothing to this one.
        if not (isNull (box _root)) then
            _root.unmount()
            _root <- Unchecked.defaultof<IReactRoot>

        _domEl <- Unchecked.defaultof<Element>

type React =
    /// <summary>
    /// Renders a React element into a Lit template
    /// </summary>
    /// <param name="reactComponent">The function that will be called to render the component.</param>
    /// <param name="className">The class name to apply to the rendered element.</param>
    /// <returns>A <see cref="Lit.TemplateResult">TemplateResult</see></returns>
    static member toLit (reactComponent: 'Props -> ReactElement, ?className: string): 'Props -> TemplateResult =
        emitJsExpr (jsConstructor<ReactDirective>, reactComponent, defaultArg className "")
            "class extends $0 { renderFn = $1; className = $2 }"
        |> LitBindings.directive :?> _

    /// <summary>
    /// Renders a Lit template into a React element
    /// </summary>
    /// <param name="template">A Lit template result.</param>
    /// <param name="tag">the name of the tag that will wrap the Lit template result .</param>
    /// <param name="className">a class name for the wrapper element.</param>
    /// <returns>A ReactElement</returns>
    static member inline ofLit (template: TemplateResult, ?tag: string, ?className: string) =
        let tag = defaultArg tag "div"
        let container = Hooks.useRef Unchecked.defaultof<Element option>
        Hooks.useEffect((fun () ->
            match container.current with
            | None -> ()
            | Some el -> template |> Lit.render (el :?> HTMLElement)
        ))
        domEl tag [
            Props.Class (defaultArg className "")
            Props.RefValue container
        ] []

    /// Renders a Lit HTML template as a ReactElement.
    /// Must be used at the root of a React functional component (like a hook).
    static member inline lit_html (s: FormattableString) =
        React.ofLit(Lit.html s)

    /// Renders a Lit SVG template as a ReactElement.
    /// svg is required for nested templates within an svg element.
    /// Must be used at the root of a React functional component (like a hook).
    static member inline lit_svg (s: FormattableString) =
        React.ofLit(Lit.html s)
