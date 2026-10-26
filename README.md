# Fable.Lit

[![Fable.Lit](https://img.shields.io/nuget/v/Fable.Lit.svg?label=Fable.Lit)](https://www.nuget.org/packages/Fable.Lit/)
[![Fable.Lit.Elmish](https://img.shields.io/nuget/v/Fable.Lit.Elmish.svg?label=Fable.Lit.Elmish)](https://www.nuget.org/packages/Fable.Lit.Elmish/)

Fable.Lit is a collection of tools to help you write [Fable](https://fable.io/) apps by embedding HTML code into your F# code with the power of [Lit](https://lit.dev/).

**Documentation:** https://fable-hub.github.io/Fable.Lit/

## Status

Fable.Lit 2.0 is a maintenance release: it supports **Fable 5**, **Lit 3** and **Elmish 5**, with no API changes from 1.x. New features will be considered now that it's out; proposals are labeled [after-2.0](https://github.com/fable-hub/Fable.Lit/labels/after-2.0). See [#90](https://github.com/fable-hub/Fable.Lit/issues/90) for the plan.

| Package | Status |
|---|---|
| Fable.Lit | 2.0, maintained |
| Fable.Lit.Elmish | 2.0, maintained |
| Fable.Lit.Test | Waiting on Fable.Expect support for Fable 5 |
| Fable.Lit.React | To be revisited after 2.0 |
| Fable.Lit.Feliz | No new releases planned; see [Fable.Lit.Dsl](https://github.com/JordanMarr/Fable.Lit.Dsl) for a typed DSL |
| ConstructStyleSheetsPolyfill | No longer needed: all current browsers support constructable stylesheets |

## Development

Before doing anything make sure to install the dependencies after cloning the repository by running:

`npm install`

## How to test locally ?

`npm run test`

## How to publish a new version of the package ?

Releases are made by [EasyBuild.ShipIt](https://github.com/easybuild-org/EasyBuild.ShipIt) from [Conventional Commits](https://www.conventionalcommits.org/), so pull requests are squash-merged and their title becomes the commit message (`fix: ...`, `feat: ...`).

1. After each push to `main` that passes the tests, ShipIt opens (or updates) a `chore: release ...` pull request with the new versions and `CHANGELOG.md` entries.
2. Merging that pull request publishes the packages to NuGet.

Only the packages with a `CHANGELOG.md` are released: Fable.Lit and Fable.Lit.Elmish.

To preview the next release locally: `dotnet shipit --mode local --skip-invalid-commit`, then `git restore` the changed files.

## How to work on the documentation ?

The docs toolchain ([Nacara](https://github.com/MangelMaxime/Nacara) 1.0 beta) needs **Node 14**: on newer versions of Node it fails with `ENOENT ... lstat '.nacara'`. If you don't have Node 14 installed, `npx -y node@14 node_modules/nacara/cli.js watch` runs it without switching your Node version.

1. `npm run docs:watch`
2. Go to [http://localhost:8080/](http://localhost:8080/)

## How to update the documentation ?

The "Publish" workflow deploys the site to GitHub Pages whenever a change to `docsrc/` or `nacara.config.json` is pushed to `main`. It can also be run by hand from the Actions tab.
