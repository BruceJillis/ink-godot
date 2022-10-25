# godot-ink

An [ink](https://github.com/inkle/ink) integration for [Godot Engine](https://github.com/godotengine/godot).  Based on 3.2 code by [Paulloz](https://github.com/paulloz/godot-ink/).

## Installation

The installation process is a bit brittle at the moment, but as long as you're doing everything in order, everything should be alright.

1. Install *godot-ink* by dropping the `addons/ink-godot/` folder in your project's `addons/` folder.
1. Check your project contains a `.csproj` file.
	* If not, there's a menu for that in Godot:  
	`Project → Tools → Mono → Create C# Solution`
1. Download the last [ink release](https://github.com/inkle/ink/releases).
1. Drop the `ink-engine-runtime.dll` at the root of your project.
1. Reference said file in your `.csproj` file.
	```xml
		<ItemGroup>
			<Reference Include="ink-engine-runtime">
				<HintPath>ink-engine-runtime.dll</HintPath>
			</Reference>
		</ItemGroup>
	```
1. Build your project.
1. Enable *godot-ink* in the plugins tab of the project settings window.

## License

*ink-godot* is released under MIT license (see the [LICENSE](/LICENSE) file for more information).
