---
last_commit_released: 20eb628c7e2fcabce2584507642920f047cc8a77
name: Fable.Lit
updaters:
  - regex:
      file: Lit.fsproj
      pattern: (?<=<Version>).*(?=</Version>)
---

# Changelog

All notable changes to this project will be documented in this file.

This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This changelog is generated using [EasyBuild.ShipIt](https://github.com/easybuild-org/EasyBuild.ShipIt).

⚠ Only edit the front matter metadata at the top of this file. All other changes will be overwritten when a new release is created.

## 2.0.0 - 2026-10-19

### 🏗️ Breaking changes

* Support Fable 5, Lit 3 and Elmish 5 ([d3bd011](https://github.com/fable-hub/Fable.Lit/commit/d3bd0115b66d61d9fb0861b40b79341b1b51e1fa))

### 🐞 Bug Fixes

* Re-establish hook effects when a LitElement is moved or reconnected (#81) ([d69bc76](https://github.com/fable-hub/Fable.Lit/commit/d69bc76fd47d8d98d78f59bdbac1bbe2f353d173))

<strong><small>[View changes on Github](https://github.com/fable-hub/Fable.Lit/compare/b08eb88bc057f5c8eb5f1cf40f87cf87b48b909b..20eb628c7e2fcabce2584507642920f047cc8a77)</small></strong>

## 1.4.2 - 2022-07-14

* `Lit.mapiUnique` @leolorenzoluis
* `styles` binding @AngelMunoz
* Fix #39: Better error message for unsafe CSS
* Fix #31: Conflict with `name` attribute

## 1.4.1 - 2021-11-23

* Add Hook.use_scoped_css
* Hook.useTransition now gives class name instead of css
* Make Hook.useHmr work with multiple components

## 1.3.2 - 2021-11-13

* HMR improvements

## 1.3.1 - 2021-11-04

* Fix `useEffectOnce` running twice on LitElements

## 1.3.0 - 2021-10-11

* Remove custom useElmish implementation
* Fix #22: Add renderRoot to LitElement

## 1.2.0 - 2021-10-03

* Add ofImport
* Clean up API

## 1.1.0 - 2021-10-01

* useShadowDOM setting for LitElements
* HMR support for LitElement

## 1.0.0 - 2021-09-27

* Stable release
