---
last_commit_released: 20eb628c7e2fcabce2584507642920f047cc8a77
name: Fable.Lit.Elmish
depends_on:
  - ../Lit/
updaters:
  - regex:
      file: Lit.Elmish.fsproj
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

### 🔗 Dependencies

* `Fable.Lit` updated to 2.0.0

<strong><small>[View changes on Github](https://github.com/fable-hub/Fable.Lit/compare/60a1f8e74ea8b862eb56a2477f84fc563847aaa5..20eb628c7e2fcabce2584507642920f047cc8a77)</small></strong>

## 1.4.0 - 2021-11-13

* Remove HMR support in favor of Fable.Elmish.HMR

## 1.3.0 - 2021-10-11

* Implement useElmish using Elmish.Program

## 1.2.0 - 2021-10-03

* Add Hook.useElmish(program)

## 1.0.0 - 2021-09-27

* Stable release
