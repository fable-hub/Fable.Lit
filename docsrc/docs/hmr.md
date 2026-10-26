---
title: Hot Reloading
layout: nacara-standard
---

## Webpack

Webpack has been the standard to build frontend apps for a while, and it's also widely used in Fable apps. Webpack is extremely reliable and extensible, and it's still a great choice for your web app.

It has a couple of disadvantages however: usually requires a configuration file that can get complicated very quickly and its development server bundles your whole app for every change, which can take a few seconds in large projects.

## Vite

A new generation of JS tooling is appearing which follow best practices by default so they require little to no configuration. Moreover, these tools include a development server that doesn't bundle your application, which makes updated much faster as the browser only need to load the modules that have changed. The most prominent example is [Vite](https://vite.dev).

We've personally got great results with Vite. It is somewhat opinionated and thanks to this you can get a development server and build tool with zero-config by conforming to a few defaults:

- Put your static files in the `public` folder
- Put your `index.html` in the app root (usually next to `package.json`)
- Add a reference to the entry JS file (relative path is important):

```html
<script type="module" src="./build/client/App.js"></script>
```

With this you only need to install Vite and run the commands for starting the development server or bundling for production as needed.

```sh
npm i -D vite
npx vite --open     # Start dev server and open browser
npx vite build      # Build your site for production
```

That's it, now you have an extremely fast development server and an optimized build with zero config (built files will be put in `dist` folder by default). If you need to edit some settings, you can still use a [config file](https://vitejs.dev/config/) or the CLI options (check them with `npx vite --help`).

## Elmish and Local HMR

For Elmish apps, whole-program HMR is provided by [Elmish HMR](https://elmish.github.io/hmr/), which Fable.Lit.Elmish 1.x was used with. It hasn't been tested with Fable.Lit.Elmish 2 and Elmish 5 yet.

Fable.Lit also provides some code helpers to enable HMR support for local component hooks, like `useState` or `useElmish` (only compatible with Vite at the time of writing). To this effect, the first thing you need to do is to instantiate a private _HMR token_ at the top of each file containing Fable.Lit components:

```fsharp
open Lit

let private hmr = HMR.createToken()
```

Now pass the HMR token to each component you want to enable HMR for with the `useHmr` hook.

```fsharp
[<HookComponent>]
let MyComponent() =
    Hook.useHmr(hmr)
    ..
```

<br />

This is a bit of a set up but it will enable a blazing fast feedback loop that is extremely valuable when you're making small UI adjustments. Also, when compiling in Release mode the HMR helpers will automatically be removed, so you don't need to worry about disabling them manually.

<hr />

If for some reason you are getting unexpected behavior with HMR, you can disable it and force a full refresh by passing `false` when creating the HMR token in a specific module.

```fsharp
let hmr = HMR.createToken(false)
```
