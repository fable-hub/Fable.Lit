/// Custom elements that take part in forms.
///
/// A custom element is invisible to the form it sits in unless the class says
/// `static formAssociated = true`. With it, the element is handed an ElementInternals,
/// through which it can find its form, contribute a value to submission, and hear about
/// resets. `config.formAssociated <- true` is that line.
///
/// The test is the thing people actually build first: a submit button that is not a
/// <button>, which needs the form before it can ask it to submit.
module FormElementTest

open Browser
open Fable.Core
open Fable.Core.JsInterop
open Lit
open WebTestRunner

[<Emit("customElements.get($0)")>]
let private definitionOf (tag: string) : obj = jsNative

[<LitElement("fel-submit-button")>]
let SubmitButton () =
    let host, _ = LitElement.init (fun config -> config.formAssociated <- true)

    // ElementInternals is where the form lives. Without formAssociated the call still
    // succeeds, but reading `form` from it throws NotSupportedError.
    if isNull (box host?internals) then
        host?internals <- host?attachInternals ()

    html $"""<button type="button" @click={Ev(fun _ -> host?internals?form?requestSubmit ())}>Go</button>"""

[<LitElement("fel-ordinary")>]
let Ordinary () =
    LitElement.init () |> ignore
    html $"""<p>not a form control</p>"""

describe "Form-associated elements" <| fun () ->

    it "declares itself to the form, and only when asked to" <| fun () -> promise {
        // The decorator defines the element when this module loads; calling the function
        // is what raises, since a LitElement is created by the browser from its tag.
        do! Promise.sleep 100

        if definitionOf("fel-submit-button")?formAssociated <> true then
            failwith "the element was defined without formAssociated, so no form will see it"

        if definitionOf("fel-ordinary")?formAssociated = true then
            failwith "an element that never asked for it was made form-associated"
    }

    it "submits the form it belongs to" <| fun () -> promise {
        let host = document.createElement "div"
        document.body.appendChild host |> ignore
        host.innerHTML <- """<form id="f"><fel-submit-button></fel-submit-button></form>"""

        let form = host.querySelector "#f" :?> Browser.Types.HTMLFormElement
        let mutable submits = 0

        form.addEventListener (
            "submit",
            fun ev ->
                ev.preventDefault ()
                submits <- submits + 1
        )

        do! Promise.sleep 100

        let el = host.querySelector "fel-submit-button"
        let button = el?shadowRoot?querySelector ("button")

        if isNull (box button) then
            failwith "the element never rendered"

        // requestSubmit runs the form's validation and fires submit, which is the whole
        // reason to be form-associated rather than to dispatch an event and hope.
        button?click ()
        do! Promise.sleep 100

        if submits <> 1 then
            failwith $"the button did not submit the form it is inside ({submits} submits)"

        document.body.removeChild host |> ignore
    }
