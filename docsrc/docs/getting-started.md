---
title: Getting started
layout: nacara-standard
---

## Requirements

Fable.Lit 2 requires the **Fable 5** dotnet tool and **Lit 3** from npm (Lit 2 also works). Fable 4 should work too but hasn't been tested. Fable.Lit.Elmish 2 requires **Elmish 5**.

## Installation

In the directory of your .fsproj, install the packages you need and Lit from npm. Note the package ids are prefixed by `Fable.` but not the actual namespaces.

```
dotnet add package Fable.Lit
dotnet add package Fable.Lit.Elmish
npm install lit
```

:::info
If you use [Femto](https://github.com/Zaid-Ajaj/Femto), `dotnet femto install Fable.Lit` installs the npm dependency for you.
:::

Fable.Lit.Test hasn't been released for Fable 5 yet, see [Testing](./testing.html).

## Scaffolding

To get up-to-speed faster you can start from [fable-lit-fullstack-template](https://github.com/JordanMarr/fable-lit-fullstack-template), a full-stack F# template with Fable.Lit web components, the typed [Fable.Lit.Dsl](https://github.com/JordanMarr/Fable.Lit.Dsl) and type-safe client/server RPC. [Lit.TodoMVC](https://github.com/alfonsogarciacaro/Lit.TodoMVC) is a smaller example including HTML templates, hook components, hot reloading and testing, but it was written for Fable 3 and Fable.Lit 1.
