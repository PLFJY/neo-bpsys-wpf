# Example Fronted Controls

This DEBUG-only built-in plugin demonstrates how a plugin registers Designer v3 fronted controls.

## What it registers

- Plugin id: `top.plfjy.example.fronted`
- Control: `plugin:top.plfjy.example.fronted/TeamCard`
- Contributor: `TeamCardFrontedControlContributor`
- Config: `TeamCardFrontedControlConfig`, inheriting `FrontedControlConfigBase`

The plugin registers an `IFrontedControlPluginContributor` during `Plugin.Initialize`, then supplies a descriptor with:

- typed config
- runtime `CreateControl`
- `CreateDefaultConfig`
- display and description localization keys
- declarative property metadata for binding, color, number, and enum editors

`TeamCardFrontedControlConfig` defaults to Binding Browser paths that exist in the current Designer v3 catalog:

- `CurrentGame.SurTeam.Name`
- `CurrentGame.SurTeam.Logo`

## Test in Debug

Build the main app in `Debug`. The main project copies this plugin to:

```text
Plugins/top.plfjy.example.fronted
```

Start the app, open `FrontedDesignerWindow`, open Add Control, and add `TeamCard` from the plugin controls group.
Saving or exporting a layout that contains `TeamCard` should write `RequiredPlugins.MinVersion` / `PluginDependencies.MinVersion` from this plugin's `manifest.yml` version. The exported `.bpui` package must only contain layout metadata and resources; it must not contain this plugin's DLLs or zip package.

The sample is not included in Release, Beta, or Preview output by default.

## Do not use legacy designer APIs

Designer v3 edits JSON layout documents in the independent editor. Legacy real-window designer APIs are removed/deprecated for this workflow.

Do not build new controls around:

- `DesignBehavior`
- `CanvasAdorner`
- `DesignerModeChangedMessage`
- `IsDesignerMode`
- `FrontElementsConfig`
- `SaveWindowElementsPosition`
- `RestoreInitialPositions`
