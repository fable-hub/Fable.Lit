/// A minimal ASP.NET server that renders the page with Fable.Lit.Server, per request.
///
/// The point of the sample is the two lines in the handler: the same F# view the browser
/// runs is rendered here to HTML with lit's hydration markers in it, and dropped into a
/// page template as a Node. Nothing about the view knows it is on a server.
module Server

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open HtmlTypeProvider
open Lit

type Page = Template<"page.html">

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    let app = builder.Build()

    // The bundled client, built by vite from the Fable output. Bundled rather than
    // served raw because the compiled JavaScript imports "lit" by name, and a browser
    // needs that resolved by something.
    app.UseStaticFiles() |> ignore

    app.MapGet(
        "/",
        System.Func<IResult>(fun () ->
            // `toHydratableNode`, not `toNode`: the same markup plus the comment markers
            // lit needs to find its bindings again. The count starts at 0 and the
            // handler does nothing, because a server has no handlers; the client
            // supplies both, and must supply the same count.
            let content = toHydratableNode (Views.page 0 ignore)
            Results.Content(Page().Content(content).Render(), "text/html"))
    )
    |> ignore

    app.Run("http://localhost:5199")
    0
