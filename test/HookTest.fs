module HookTest

open Fable.Core
open Browser
open Elmish
open Lit
open Lit.Elmish
open Expect
open Expect.Dom
open WebTestRunner
open Lit.Test

[<Emit("window.getComputedStyle($1).getPropertyValue($0)")>]
let getComputedStyle (prop: string) (el: Browser.Types.Element): string = jsNative

let mutable pings = 0

[<Emit("document.dispatchEvent(new Event('hook-test-ping'))")>]
let dispatchPing (): unit = jsNative

/// Counts events two ways: into component state through the render closure, and into a
/// module-level mutable that outlives the component.
[<HookComponent>]
let PingCounter () =
    let value, setValue = Hook.useState 0

    Hook.useEventListener(document, "hook-test-ping", fun (_: Browser.Types.Event) ->
        pings <- pings + 1
        setValue (value + 1))

    html $"""<p id="ping-count">{value}</p>"""

/// Renders the counter away on demand. Removing the host element from the document
/// would not do: lit has no MutationObserver, so a directive is disconnected when a
/// render stops including it, not when somebody detaches its DOM.
[<HookComponent>]
let PingCounterContainer () =
    let removed, setRemoved = Hook.useState false

    html
        $"""
      <div>
        <button @click={Ev(fun _ -> setRemoved true)}>Remove!</button>
        {if not removed then PingCounter() else Lit.nothing}
      </div>
    """

[<HookComponent>]
let Counter () =
    let value, setValue = Hook.useState 5

    html
        $"""
      <div>
        <!-- Check snapshot can contain # char. See https://github.com/modernweb-dev/web/issues/1690 -->
        <p>F# counter</p>
        <p>Value: {value}</p>
        <button @click={Ev(fun _ -> value + 1 |> setValue)}>Increment</button>
        <button @click={Ev(fun _ -> value - 1 |> setValue)}>Decrement</button>
      </div>
    """

[<LitElement("test-counter")>]
let CounterW () =
    let _ = LitElement.init()
    let value, setValue = Hook.useState 5

    html
        $"""
      <div>
        <!-- Check snapshot can contain # char. See https://github.com/modernweb-dev/web/issues/1690 -->
        <p>F# counter</p>
        <p>Value: {value}</p>
        <button @click={Ev(fun _ -> value + 1 |> setValue)}>Increment</button>
        <button @click={Ev(fun _ -> value - 1 |> setValue)}>Decrement</button>
      </div>
    """

[<HookComponent>]
let Input() =
    let value, setValue = Hook.useState "World"

    html
        $"""
      <div>
        <p>Hello {value}!</p>
        <label>
            Name
            <input type="text" @input={EvVal setValue}>
        </label>
      </div>
    """

[<LitElement("test-input")>]
let InputW() =
    let _ = LitElement.init()
    let value, setValue = Hook.useState "World"

    html
        $"""
      <div>
        <p>Hello {value}!</p>
        <label>
            Name
            <input type="text" @input={EvVal setValue}>
        </label>
      </div>
    """

[<HookComponent>]
let Disposable (r: ref<int>) =
    let value, setValue = Hook.useState 5

    Hook.useEffectOnce
        (fun () ->
            r.Value <- r.Value + 1
            Hook.createDisposable (fun () -> r.Value <- r.Value + 10))

    html
        $"""
      <div>
        <p>Value: {value}</p>
        <button @click={Ev(fun _ -> value + 1 |> setValue)}>Increment</button>
        <button @click={Ev(fun _ -> value - 1 |> setValue)}>Decrement</button>
      </div>
    """

[<HookComponent>]
let DisposableContainer (r: ref<int>) =
    let disposed, setDisposed = Hook.useState false

    html
        $"""
      <div>
        <button @click={Ev(fun _ -> setDisposed true)}>Dispose!</button>
        {if not disposed then
             Disposable(r)
         else
             Lit.nothing}
      </div>
    """

[<LitElement("test-disposable")>]
let DisposableW () =
    let _, props = LitElement.init(fun cfg ->
        cfg.props <- {| r = Prop.Of(ref 0, attribute="") |}
    )
    let r = props.r.Value
    let value, setValue = Hook.useState 5

    Hook.useEffectOnce
        (fun () ->
            r.Value <- r.Value + 1
            Hook.createDisposable (fun () -> r.Value <- r.Value + 10))

    html
        $"""
      <div>
        <p>Value: {value}</p>
        <button @click={Ev(fun _ -> value + 1 |> setValue)}>Increment</button>
        <button @click={Ev(fun _ -> value - 1 |> setValue)}>Decrement</button>
      </div>
    """

[<LitElement("test-disposable-container")>]
let DisposableContainerW () =
    let _, props = LitElement.init(fun cfg ->
        cfg.props <- {| r = Prop.Of(ref 0, attribute="") |}
    )
    let r = props.r.Value
    let disposed, setDisposed = Hook.useState false

    let body =
        if not disposed then html $"<test-disposable .r={r} />"
        else Lit.nothing

    html
        $"""
      <div>
        <button @click={Ev(fun _ -> setDisposed true)}>Dispose!</button>
        {body}
      </div>
    """

[<HookComponent>]
let MemoizedValue () =
    let state, setState = Hook.useState (10)
    let second, setSecond = Hook.useState (0)
    let value = Hook.useMemo (fun _ -> second + 1)

    html
        $"""
        <div>
            <p id="state">{state}</p>
            <p id="second">{second}</p>
            <p id="memoized">{value}<p>
            <button @click={Ev(fun _ -> setState (state + 1))}>State</button>
            <button @click={Ev(fun _ -> setSecond (second + 1))}>Second</button>
        </div>
        """

[<LitElement("test-memoized")>]
let MemoizedValueW () =
    let _ = LitElement.init()
    let state, setState = Hook.useState (10)
    let second, setSecond = Hook.useState (0)
    let value = Hook.useMemo (fun _ -> second + 1)

    html
        $"""
        <div>
            <p id="state">{state}</p>
            <p id="second">{second}</p>
            <p id="memoized">{value}<p>
            <button @click={Ev(fun _ -> setState (state + 1))}>State</button>
            <button @click={Ev(fun _ -> setSecond (second + 1))}>Second</button>
        </div>
        """

type private State = { counter: int; reset: bool }

type private Msg =
    | Increment
    | IncrementAgain
    | Decrement
    | DelayedReset
    | AfterDelay

let private init () = { counter = 0; reset = false }, Cmd.none

let private update msg state =
    match msg with
    | Increment ->
        { state with
              counter = state.counter + 1 },
        Cmd.none
    | IncrementAgain ->
        { state with
              counter = state.counter + 1 },
        Cmd.ofMsg Increment
    | Decrement ->
        { state with
              counter = state.counter - 1 },
        Cmd.none
    | DelayedReset -> state, Cmd.OfAsync.perform (fun (t: int) -> Async.Sleep t) 200 (fun _ -> AfterDelay)
    | AfterDelay -> { state with counter = 0; reset = true }, Cmd.none

[<HookComponent>]
let ElmishComponent () =
    let state, dispatch = Hook.useElmish(init, update)

    let resetDisplay() =
        if state.reset then html $"""<p id="reset">Has reset!</p>"""
        else Lit.nothing

    html
        $"""
            <div>
               <p id="count">{state.counter}</p>
               {resetDisplay()}
               <button @click={Ev(fun _ -> dispatch Increment)}>Increment</button>
               <button @click={Ev(fun _ -> dispatch IncrementAgain)}>Increment Again</button>
               <button @click={Ev(fun _ -> dispatch Decrement)}>Decrement</button>
               <button @click={Ev(fun _ -> dispatch DelayedReset)}>Delayed Reset</button>
            </div>
          """

[<LitElement("test-elmish")>]
let ElmishComponentW () =
    let _ = LitElement.init()
    let state, dispatch = Hook.useElmish(init, update)

    let resetDisplay() =
        if state.reset then html $"""<p id="reset">Has reset!</p>"""
        else Lit.nothing

    html
        $"""
            <div>
               <p id="count">{state.counter}</p>
               {resetDisplay()}
               <button @click={Ev(fun _ -> dispatch Increment)}>Increment</button>
               <button @click={Ev(fun _ -> dispatch IncrementAgain)}>Increment Again</button>
               <button @click={Ev(fun _ -> dispatch Decrement)}>Decrement</button>
               <button @click={Ev(fun _ -> dispatch DelayedReset)}>Delayed Reset</button>
            </div>
          """

[<HookComponent>]
let ScopedCss () =
    let className = Hook.use_scoped_css """
        p {
            color: rgb(35, 38, 41);
        }
    """
    html
        $"""
        <div class={className}>
            <p id="scoped">Hello</p>
        </div>
        """

describe "Hook" <| fun () ->
    it "renders Counter" <| fun () -> promise {
        use! container = Counter() |> render
        return! container.El |> Expect.matchHtmlSnapshot "counter"
    }

    it "renders Counter as LitElement" <| fun () -> promise {
        use! container = render_html $"<test-counter />"
        return! container.El |> Expect.matchHtmlSnapshot "counter"
    }

    it "renders Input" <| fun () -> promise {
        use! container = Input() |> render
        let el = container.El
        let greeting = el.getByText("hello")
        greeting |> Expect.innerText "Hello World!"
        el.getTextInput("name").focus()
        do! Wtr.typeChars("Mexico")
        greeting |> Expect.innerText "Hello Mexico!"
    }

    it "renders Input as LitElement" <| fun () -> promise {
        use! container = render_html $"<test-input />"
        let el = container.El
        let greeting = el.getByText("hello")
        greeting |> Expect.innerText "Hello World!"
        el.getTextInput("name").focus()
        do! Wtr.typeChars("Mexico")
        greeting |> Expect.innerText "Hello Mexico!"
    }

    it "increases/decreases the counter on button click" <| fun () -> promise {
        use! el = Counter() |> render
        let el = el.El
        let valuePar = el.getByText("value")
        valuePar |> Expect.innerText "Value: 5"
        let incrButton = el.getButton("increment")
        let decrButton = el.getButton("decrement")
        do! click el incrButton
        valuePar |> Expect.innerText "Value: 6"
        do! click el decrButton
        do! click el decrButton
        valuePar |> Expect.innerText "Value: 4"
    }

    it "increases/decreases the counter on button click as LitElement" <| fun () -> promise {
        use! el = render_html $"<test-counter />"
        let el = el.El
        let valuePar = el.getByText("value")
        valuePar |> Expect.innerText "Value: 5"
        let incrButton = el.getButton("increment")
        let decrButton = el.getButton("decrement")
        do! click el incrButton
        valuePar |> Expect.innerText "Value: 6"
        do! click el decrButton
        do! click el decrButton
        valuePar |> Expect.innerText "Value: 4"
    }

    it "useEffectOnce runs on mount/dismount" <| fun () -> promise {
        let aRef = ref 8
        use! el = DisposableContainer(aRef) |> render
        let el = el.El
        el.getByText("value") |> Expect.innerText "Value: 5"

        // Effect is run asynchronously after after render
        do! Promise.sleep 100
        aRef.Value |> Expect.equal 9

        // Effect is not run again on rerenders
        do! click el <| el.getButton("increment")
        el.getByText("value") |> Expect.innerText "Value: 6"
        aRef.Value |> Expect.equal 9

        // Cause the component to be dismounted
        do! click el <| el.getButton("dispose")
        // Effect has been disposed

        aRef.Value |> Expect.equal 19
    }

    // Disconnecting is not always the end of an element. Moving one -- reordering a list,
    // re-parenting a node, a drag -- is a disconnect and a connect on the same instance,
    // and `disconnectedCallback` has disposed everything `useEffectOnce` set up by the
    // time the second one arrives.
    it "useEffectOnce is set up again when a LitElement is reconnected" <| fun () -> promise {
        let aRef = ref 8
        use! container = render_html $"<test-disposable .r={aRef} />"
        let el = container.El
        let parent = el.parentElement

        do! Promise.sleep 100
        aRef.Value |> Expect.equal 9

        // Out of the document: the effect is disposed, as it already was.
        parent.removeChild el |> ignore
        do! Promise.sleep 100
        aRef.Value |> Expect.equal 19

        // Back in, as the same element. Without a connectedCallback this stays at 19.
        parent.appendChild el |> ignore
        do! Promise.sleep 100
        aRef.Value |> Expect.equal 20
    }

    it "useEffectOnce runs on mount/dismount as LitElement" <| fun () -> promise {
        let aRef = ref 8
        use! el = render_html $"<test-disposable-container .r={aRef} />"
        let el = el.El
        assert false
        el.getByText("value") |> Expect.innerText "Value: 5"

        // Effect is run asynchronously after after render
        do! Promise.sleep 100
        aRef.Value |> Expect.equal 9

        // Effect is not run again on rerenders
        do! click el <| el.getButton("increment")
        el.getByText("value") |> Expect.innerText "Value: 6"
        aRef.Value |> Expect.equal 9

        // Cause the component to be dismounted
        do! click el <| el.getButton("dispose")
        // Effect has been disposed

        aRef.Value |> Expect.equal 19
    }

    it "useMemo doesn't change without dependencies" <| fun () -> promise {
        use! el = MemoizedValue() |> render
        let el = el.El
        let state = el.getButton("state")
        let second = el.getButton("second")

        do! click el state
        el.getSelector("#state") |> Expect.innerText "11"
        // un-related re-renders shouldn't affect memoized value
        el.getSelector("#memoized") |> Expect.innerText "1"
        // change second value trice
        do! click el second
        do! click el second
        do! click el second

        // second should have changed
        el.getSelector("#second") |> Expect.innerText "3"
        // memoized value should have not changed
        el.getSelector("#memoized") |> Expect.innerText "1"
    }

    it "useMemo doesn't change without dependencies as LitElement" <| fun () -> promise {
        use! el = render_html $"<test-memoized />"
        let el = el.El
        let state = el.getButton("state")
        let second = el.getButton("second")

        do! click el state
        el.getSelector("#state") |> Expect.innerText "11"
        // un-related re-renders shouldn't affect memoized value
        el.getSelector("#memoized") |> Expect.innerText "1"
        // change second value trice
        do! click el second
        do! click el second
        do! click el second

        // second should have changed
        el.getSelector("#second") |> Expect.innerText "3"
        // memoized value should have not changed
        el.getSelector("#memoized") |> Expect.innerText "1"
    }

    it "useElmish dispatches messages correctly" <| fun () -> promise {
        use! el = ElmishComponent() |> render
        let el = el.El
        let inc = el.getButton("increment")
        let incAgain = el.getButton("increment again")
        let decr = el.getButton("decrement")
        let delayedReset = el.getButton("delay")

        // normal dispatch works
        el.getSelector("#count") |> Expect.innerText "0"
        do! click el inc
        do! click el inc
        el.getSelector("#count") |> Expect.innerText "2"

        // Cmd works, see https://github.com/fable-compiler/fable-promise/issues/24#issuecomment-934328900
        do! click el incAgain
        el.getSelector("#count") |> Expect.innerText "4"

        // normal dispatch works
        do! click el decr
        do! click el decr
        do! click el decr
        do! click el decr
        el.getSelector("#count") |> Expect.innerText "0"

        // normal dispatch works
        do! click el decr
        do! click el decr
        el.getSelector("#count") |> Expect.innerText "-2"

        do! click el delayedReset
        el.getSelector("#count") |> Expect.innerText "-2"
        let! _reset =
            Expect.retryUntil "reset text appears" (fun () ->
                el.getSelector("#reset"))
        // dispatch with async cmd works
        el.getSelector("#count") |> Expect.innerText "0"
    }

    it "useElmish dispatches messages correctly as LitElement" <| fun () -> promise {
        use! el = render_html $"<test-elmish />"
        let el = el.El
        let inc = el.getButton("increment")
        let incAgain = el.getButton("increment again")
        let decr = el.getButton("decrement")
        let delayedReset = el.getButton("delay")

        // normal dispatch works
        el.getSelector("#count") |> Expect.innerText "0"
        do! click el inc
        do! click el inc
        el.getSelector("#count") |> Expect.innerText "2"

        // Cmd works, see https://github.com/fable-compiler/fable-promise/issues/24#issuecomment-934328900
        do! click el incAgain
        el.getSelector("#count") |> Expect.innerText "4"

        // normal dispatch works
        do! click el decr
        do! click el decr
        do! click el decr
        do! click el decr
        el.getSelector("#count") |> Expect.innerText "0"

        // normal dispatch works
        do! click el decr
        do! click el decr
        el.getSelector("#count") |> Expect.innerText "-2"

        do! click el delayedReset
        el.getSelector("#count") |> Expect.innerText "-2"
        let! _reset =
            Expect.retryUntil "reset text appears" (fun () ->
                el.getSelector("#reset"))
        // dispatch with async cmd works
        el.getSelector("#count") |> Expect.innerText "0"
    }

    it "Scoped CSS works" <| fun () -> promise {
        use! _container =
            html $"""
            <p id="non-scoped">Hello</p>
            {ScopedCss()}
            """
            |> render

        let nonScopedColor =
            document.getElementById("non-scoped")
            |> getComputedStyle "color"

        let scopedColor =
            document.getElementById("scoped")
            |> getComputedStyle "color"

        Expect.equal "rgb(35, 38, 41)" scopedColor
        Expect.notEqual nonScopedColor scopedColor
    }

    // Two things at once, because they are the two ways this hook can be wrong.
    //
    // The count proves the handler is not a stale closure. It reads `value` from the
    // render it was written in, so a listener bound once at connect time and never
    // refreshed would keep adding one to zero, and the second event would still read 1.
    //
    // `pings` proves the listener is really removed. It counts calls in a mutable the
    // component does not own, so it keeps counting after the component is gone, which
    // is exactly what a leaked listener would do.
    it "useEventListener sees the latest render, and stops on disconnect" <| fun () -> promise {
        pings <- 0
        use! container = PingCounterContainer() |> render
        let el = container.El

        dispatchPing ()
        do! elementUpdated el
        el.getSelector("#ping-count") |> Expect.innerText "1"

        dispatchPing ()
        do! elementUpdated el
        el.getSelector("#ping-count") |> Expect.innerText "2"

        do! click el <| el.getButton("remove")
        do! Promise.sleep 100

        dispatchPing ()
        do! Promise.sleep 0
        pings |> Expect.equal 2
    }
