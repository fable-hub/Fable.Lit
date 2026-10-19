/// Writes what .NET makes of the shared views, for the browser to check against.
///
/// The comparison happens in the browser rather than here, and deliberately: only a
/// browser can say whether two pieces of markup mean the same thing. lit renders into
/// the DOM and litters it with its own marker comments, so a string comparison would be
/// measuring the markers. The browser parses this output into a second DOM and compares
/// the trees with isEqualNode, which is the question actually worth asking.
module Lit.Server.Tests.Differential

open System
open System.IO
open System.Text.Json
open Xunit
open Lit

[<Fact>]
let ``render every shared case for the browser to compare`` () =
    // Both the markup and lit's digest of the template. The digest is what a
    // `<!--lit-part ...-->` marker carries and what hydrate() checks, so a port that is
    // one bit out is a hydration that throws in production. The browser checks it
    // against lit's own function.
    let rendered =
        SharedViews.cases
        |> List.collect (fun (name, template) ->
            [ name, render template
              name + "#digest", digest template
              // The node indices of elements carrying attribute bindings. lit finds
              // these by walking its own generated HTML, so the browser can produce the
              // same list from lit and hold the two side by side.
              name + "#nodes", String.Join(",", attributeElementIndices template |> Array.map string)
              // The same markup with lit's hydration markers, for the adoption test.
              name + "#hydratable", renderHydratable template ])
        |> dict

    // Repo root from the test binary: bin/Debug/net10.0 -> test/Lit.Server.Tests -> test
    let root = Path.Combine(__SOURCE_DIRECTORY__, "..")
    let target = Path.Combine(root, "server-rendered.json")
    File.WriteAllText(target, JsonSerializer.Serialize(rendered, JsonSerializerOptions(WriteIndented = true)))

    // A guard, so a silently empty file cannot pass as agreement in the browser.
    Assert.NotEmpty(SharedViews.cases)
    // Markup and digests must be non-empty; an empty node list is a real answer, since
    // a template can carry no attribute bindings at all.
    for KeyValue(name, value) in rendered do
        if not (name.EndsWith "#nodes") then
            Assert.False(String.IsNullOrWhiteSpace value, $"{name} rendered nothing")
