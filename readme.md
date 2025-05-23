# Summary

Weaver is a mod that roughly doubles the performance of DSP. The mod does not alter gameplay, does not disable achievements and can be enabled/disabled anytime.
In my tests of late game saves, Weaver improves the game's performance by roughly 2.5-3.0x.
You'll experience the best performance while flying in outer space. You can read why in the **_How it works_** section.
 
# Mod compatibility

Known compatible mods:
* SphereOpt
* DSPOptimizations
* GalacticScale

Known incompatible mods:
* SampleAndHoldSim
* GenesisBook
* Multfuntion mod

# Known issues
None. You can [report bugs here.](https://github.com/TheAIBot/DSP_Weaver/issues)

# How it works

The general idea is to avoid doing work that isn't necessary. The game spends a lot of time updating, for example, animation data on planets the player isn't on.
The mod solves this issue by "optimizing" planets the player isn't currently on. Whenever a player flies to a planet, the planet's logic is restored to the game's default logic to ensure 
all animation and UI logic functions as expected. Anything being built, upgraded or destroyed on a planet will also, briefly, cause the planet's logic to revert to the game's slower logic.
Whenever a research completes, the mod will reoptimize all planets one at a time to ensure the optimized planet includes any stat changes from the research.


# Optimizations

* Assemblers - Assemblers, Smelters, Chemical plants, Oil Refineries, Particle colliders, 
	* Optimized data format
		* Recipe and assembler tier data is relegated to a separate `struct` that is shared by all assemblers using the same recipe and tier.
	* Can go inactive when they are unable to function due to missing items to craft with or due to the output being full.
	* Parallelized logic on a sub-factory basis.
* Sorters
	* Two optimized data formats
		* Bidirectional sorters
		* Non-bidirectional sorters
		* Each format relegates tier information to a separate `struct` that is shared by all sorters in that tier.
	* Optimized data access when interacting with entities
		* Optimized access to belts, assemblers and labs
	* Sorters can go inactive when the entities they interact with are unable to provide/accept the items transferred.
	* Parallelized logic on a sub-factory basis.
* Labs
	* Two optimized data formats.
		* Labs producing science.
		* Labs consuming science.
		* Can go inactive when they are unable to function due to missing items to craft with or due to the output being full.
	* Parallelized logic on a sub-factory basis.
* Belts
	* Parallelized logic on a sub-factory basis.
		* All locks in belt code are gone.
	* Items stored on belts take up less memory.
* Spray Coaters
	* Parallelized logic on a sub-factory basis.
	* Optimized data format.
		* Optimized access to sprayer belt and sprayed belt.
	* Optimized access to belts.
* Monitors
	* Optimized access to belts.
	* Parallelized logic on a sub-factory basis.
* Fractionators
	* Optimized data format.
	* Optimized access to belts.
	* Parallelized logic on a sub-factory basis.
* Power system
	* Optimized data format.
	* Optimized power networks
		* Optimized Power Exchangers.
			* Optimized data format.
			* Optimized access to belts.
		* Optimized Ray Receivers.
			* Optimized data format
			* Optimized access to belts.
* Splitters, Pilers and Monitors
	* Optimized data formats.
	* Parallelized logic on a sub-factory basis.
	* Optimized access to belts.
* Miners
	* Optimized data formats for all miners.
		* Oil Extractor.
		* Water Pump.
		* Mining Machine.
			* Optimized access to belts.
		* Advanced Mining Machine.
			* Optimized access to belts.
	* Removed all locks on veins and mining flags.
	* Parallelized logic on a sub-factory basis.
* Depots
	* Parallelized logic on a sub-factory basis.
* Tanks
	* Optimized data format.
	* Parallelized logic on a sub-factory basis.
* Digital system
	* Parallelized logic on a sub-factory basis.
* ILS and PLS
	* Parallelized belt interaction logic on a sub-factory basis.
	* Optimized access to belts.
* Ejectors and Silos
	* Parallelized logic on a sub-factory basis.
* Turrets
	* Optimized access to belts.
* Multithreading
	* Step by step logic has been replaced with a work stealing worker system.
	* Independent factories on the same planet are considered sub factories. Sub factories are threaded independently from each other even though they are on the same planet.
* Statistics
	* No need to run statistics logic on planets that cannot generate any statistical values.

# Could DSP developers implement these optimizations?

## Spray coaters and statistics
Parallelizing spray coaters is relatively straightforward and provides a significant performance improvement in most saves. Same goes for the statistics optimizations this mod does.

## Data layout
The general implementation of DSP is quite well thought out. The use of structs of entities makes the code already quite optimized.
The biggest performance problem is that the structs are too large. Even if a sorter is on the other side of the star cluster, the sorter's animation-relevant data still has to be loaded in when the game updates the inserter.
A lot of the optimizations in this mod could be implemented by the DSP developers by making even more use of struct-of-arrays design. That's what this mod uses to reduce memory bandwidth requirements and improve cache hit rates.
Simply adding the attribute `[StructLayout(LayoutKind.Auto)]` to each of the game's large structs should improve performance and reduce memory usage.

## Deduplicating data
Additionally, a lot of the information in each struct is duplicate constant values stored in each struct instance. For example, this includes recipe requirements and entity tier data. This can easily be moved to a separate array
of structs that can be referenced, just like how this mod does it for assembler recipe information.
This type of optimization can (so far from what I've seen) be applied to the following components: assemblers, inserters, labs, consumers, `AnimData`, `PowerConsumerComponent`, `PowerGeneratorComponent` and probably a lot more.
This would also reduce the game's memory usage.

## Data access
Other optimizations are a bit more difficult. These would be the data access optimizations. These optimizations reduce the number of indirections necessary to get to the data an entity actually cares about.
It is quite easy to add an additional array that stores the network ID for each inserter so the network ID doesn't have to be fetched from `EntityData` with a random access memory request. But doing so means duplicating
information in multiple places. Updating this kind of information would require updating it in multiple places. It can quite quickly result in very complicated update logic. This mod deals with that by re-optimizing
a whole planet at a time as described in the **_How it works_** section.

## Multithreading
Before explaining how Weaver improves DSP multithreading it is important to describe how the game currently utilizes multithreading. This will make it easier to explain why the multithreading logic was changed.

### Existing multithreading
The game multithreads its logic by splitting the simulation logic up into discrete steps. A step could, for example, be to update all sorters in the game. The game manages multiple threads by creating a worker for each thread. Each worker is delegated a static amount of work up front and all workers has to complete their work before all workers can start on the next step in the simulation. This means all workers are always waiting for the slowest worker before they can start the next step. Not all CPU cores are equally fast and it is not possible to perfectly distribute the work beforehand. The end result is a mutithreading system that is not capable of 100% CPU utilization. Certain steps are also not multithreaded, like spray coaters, as previously explained.

### Weaver multithreading
Weaver analyzes how entities on each planet are connected and splits up independent factories into sub-factories. A sub factory consists of all entities that are connected. Commonly in larger games, a sub factory consists of an ILS/PLS, the belts connected to it, sorter connected to the belts and assemblers connected to the sorters.
Simulation of a sub-factory is done on a single thread. As only one thread updates a sub-factorys entities, the entities no longer need to be thread safe. Their locks and atomic operations has therefore been removed.
Splitting simulation up into sub-factories allows the game to more effectively utilize multiple cores. Cache hit rate improves as well as all data related to a sub-factory usually fits inside a cores cache.

To split the simulation up into sub-factories, the power system has to not connect entities together. Weaver handles this by updating a planets power system before updating the planets sub factories. A few other things can also not be simulated on the sub-factory level. A notable example is logistics distributors which must be updated in a certain order on each planet.
To simulate a planet, weaver splits it up into multiple steps. Firs the power system is updated. Then all sub-factories are updated. Lastly logistic distributors are updated.
To efficiently execute these steps for all planets, Weaver implements a work stealing work pool where workers compete against each other to complete steps as fast as possible. There is no synchroniation of step execution between planets like there is with the games multithreading logic.

Workers will prioritize executing work for a specific planet, unless the planet has no work immediately available. In that case the worker will attempt to find available work on other planets. Only if it can't find any immediately available work in any planet, will the worker have to wait for other workers to complete their work. A worker will attempt to reserve work in the next step before it waits. If it can't reserve work then it will find another planet to try and reserve work in. This ensures 10 threads aren't waiting in a planet where the next step only consists of 2 work chunks.

## Base game
I hope most of these optimizations are eventually merged into the base game. Then I don't have to maintain them and more players would be able to enjoy them.

# Future plans

There are still quite a few optimizations I've yet to implement.
I believe it is possible for the mod to improve the game's performance by 3-3.5x.