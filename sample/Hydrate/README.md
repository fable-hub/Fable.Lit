# Hydration sample

The same F# view rendered by .NET on the server and adopted by lit in the browser.

```bash
# from the repository root
dotnet fable sample/Hydrate/Client -o sample/Hydrate/Client/build
npx vite build --config sample/Hydrate/vite.config.mjs
dotnet run --project sample/Hydrate/Server
```

Then open <http://localhost:5199>.

## What to look at

`Shared/Views.fs` is compiled twice: by Fable against the lit bindings, and by the .NET
compiler against Fable.Lit.Server. It may use only what both provide, and it does not know
which of the two it is running as.

`Server/Program.fs` renders it with `toHydratableNode` — the same HTML as `toNode` plus
the comment markers lit uses to find its bindings again — and drops it into a page
template as a `Node`.

`Client/App.fs` calls `Hydrate.adopt`, which adopts that markup if it can and renders
over it if it cannot, saying so in the console either way.

## Proving it actually hydrated

Adoption is invisible: markup that was re-rendered looks exactly like markup that was
kept. Two ways to tell.

- Turn JavaScript off and reload. The table is still there, because .NET rendered it.
- With JavaScript on, hold a node and check it survives an update:

  ```js
  const row = document.querySelector('#app tbody tr')
  document.querySelector('#app button').click()
  document.querySelector('#app tbody tr') === row   // true: lit patched, it did not rebuild
  ```

  A console warning starting `lit could not adopt` means it fell back to a full render.

## Two things that will catch you out

**Hydrate the element the markers were written into.** The root marker wraps whatever
the template renders, so here that is `<div id="app">`, not the card inside it.
Hydrating the card puts lit inside its own root marker, where it never finds it.

**The client must pass the same template and the same data.** A different template is a
digest mismatch, which throws and is caught by `Hydrate.adopt`. Different data hydrates
cleanly and then shows values the server never rendered, which nothing catches.

Editing `page.html` and re-running is enough, but only because `Server.fsproj` carries a
`TouchOnTemplateChange` target. MSBuild does not know a `.fs` depends on an `.html`, so
without it the provider's cached output survives the rebuild and the old page keeps
being served, which looks exactly like a change that did nothing.
