## Summary

Weaver is a mod that roughly doubles the performance of DSP. The mod does not alter gameplay in any way and can be enabled/disabled anytime.
In my tests of late game saves, Weaver improves the game's performance by roughly 2-2.5x.
You'll experience the best performance while flying in outer space. You can read why in the **_How it works_** section.
 
## Mod compatibility

Known compatible mods:
* SphereOpt
* DSPOptimizations

Known incompatible mods:
* SampleAndHoldSim

## Known issues
None. You can [report bugs here.](https://github.com/TheAIBot/DSP_Weaver/issues)

## How it works

The general idea is to avoid doing work that isn't necessary. The game spends a lot of time updating, for example, animation data on planets the player isn't on.
The mod solves this issue by "optimizing" planets the player isn't currently on. Whenever a player flies to a planet, the planet's logic is restored to the game's default logic to ensure 
all animation and UI logic functions as expected. Anything being built, upgraded or destroyed on a planet will also, briefly, cause the planet's logic to revert to the game's slower logic.
Whenever a research completes, the mod will reoptimize all planets one at a time to ensure the optimized planet includes any stat changes from the research.


## Optimizations

* Assemblers - Assemblers, Smelters, Chemical plants, Oil Refineries, Particle colliders, 
	* Optimized data format
		* Recipe and assembler tier data is relegated to a separate `struct` that is shared by all assemblers using the same recipe and tier.
	* Can go inactive when they are unable to function due to missing items to craft with or due to the output being full.
* Sorters
	* Two optimized data formats
		* Bidirectional sorters
		* Non-bidirectional sorters
		* Each format relegates tier information to a separate `struct` that is shared by all sorters in that tier.
	* Optimized data access when interacting with entities
		* Optimized access to belts, assemblers and labs
	* Sorters can go inactive when the entities they interact with are unable to provide/accept the items transferred.
* Labs
	* Two optimized data formats
		* Labs producing science
		* Labs consuming science
		* Can go inactive when they are unable to function due to missing items to craft with or due to the output being full.
* Spray Coaters
	* Parallelized logic on a per-planet basis
	* Optimized data format
		* Optimized access to sprayer belt and sprayed belt.
* Power system
	* Optimized data format
	* Improved method to gather power consumer information from the above-mentioned entities.
* Statistics
	* No need to run statistics logic on planets that cannot generate any statistical values

## Could DSP developers implement these optimizations?

Parallelizing spray coaters is relatively straightforward and provides a significant performance improvement in most saves. Same goes for the statistics optimizations this mod does.

The general implementation of DSP is quite well thought out. The use of structs of entities makes the code already quite optimized.
The biggest performance problem is that the structs are too large. Even if a sorter is on the other side of the star cluster, the sorter's animation-relevant data still has to be loaded in when the game updates the inserter.
A lot of the optimizations in this mod could be implemented by the DSP developers by making even more use of struct-of-arrays design. That's what this mod uses to reduce memory bandwidth requirements and improve cache hit rates.
Simply adding the attribute `[StructLayout(LayoutKind.Auto)]` to each of the game's large structs should improve performance and reduce memory usage.

Additionally, a lot of the information in each struct is duplicate constant values stored in each struct instance. For example, this includes recipe requirements and entity tier data. This can easily be moved to a separate array
of structs that can be referenced, just like how this mod does it for assembler recipe information.
This type of optimization can (so far from what I've seen) be applied to the following components: assemblers, inserters, labs, consumers, `AnimData`, `PowerConsumerComponent`, `PowerGeneratorComponent` and probably a lot more.
This would also reduce the game's memory usage.

Other optimizations are a bit more difficult. These would be the data access optimizations. These optimizations reduce the number of indirections necessary to get to the data an entity actually cares about.
It is quite easy to add an additional array that stores the network ID for each inserter so the network ID doesn't have to be fetched from `EntityData` with a random access memory request. But doing so means duplicating
information in multiple places. Updating this kind of information would require updating it in multiple places. It can quite quickly result in very complicated update logic. This mod deals with that by re-optimizing
a whole planet at a time as described in the **_How it works_** section.

I hope most of these optimizations are eventually merged into the base game. Then I don't have to maintain them and more players would be able to enjoy them.

## Future plans

There are still quite a few optimizations I've yet to implement.
I believe it is possible for the mod to improve the game's performance by 3-3.5x.

## Why not use a patching transpiler?

Frankly I don't know how to do that. I do believe I could make use of it in some cases, but I am quite sure it isn't possible in all cases due to the size of changes this mod must make to the existing code.
I will probably get to it in the future, but right now I am having fun optimizing the game.