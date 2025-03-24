﻿# Optimizations

The games performance seems to be hamstrung by the following two issues.
	* Memory bandwith
	* Random memory access

The purpose of almost all optimizations described here is to improve upon the above two issues.
Improvements to these two areas not only make the game faster to run but also improves how well the game scales with number of CPU cores.
On my own machine the game would not scale beyond 8 threads without this mod. With this mod it now scales to 32 threads.


1. Assemblers
	* State of assembler is tracked in a separate array. Assemblers that are inactive because they are full or lack input items are not processed until their state changes.
		* States
			* Active
			* InactiveOutputFull - Assembler can not build any more due to output being full.
			* InactiveInputMissing - Assembler is missing items to build with.
		* Reduce computation and memory bandwith required of assemblers that can not do anything.
	* Network id that assembler siphons power from is stored in separate array.
		* Replaces random indirect data access with linear direct data access, reduces memory bandwith required.
	* Created new struct for assemblers that only contain what is necessary for simulation.
		* Reduced memory bandwith required to go through all assemblers.
	* Fields representing contant recipe values or assembler grade values have been moved to separate struct to reduce size of assembler struct.
		* Reduced memory bandwith required to go through all assemblers. There only exist a few instances of these that all can reside in cache.
	* Reduced all code that needed to access any entity related array.
		* Replaces random indirect data access with linear direct data access, reduces memory bandwith required.
2. Inserters
	* State and network id of inserter is stored in a separate compact `int` array.
		* States
			* Active
			* InactivePickFrom - Pick from entity is empty and inactive.
			* InactiveInsertInto - Insert into entity is full and inactive.
		* Reduce computation and memory bandwith required of inserters that can not do anything.
	* Created new structs for inserters.
		* New Structs
			* Struct for fully upgraded stack inserters.
			* Struct for all other inserters.
			* Struct for storing inserter grade related information.
		* Reduces memory bandwith required to process all inserters.
	* Inserters no longer have to read the following data from entity data. Inserter instead reads from separate arrays to get this information.
		* Optimizations
			* Which type of entity and its id it is picking items from.
			* Which type of entity and its id it is inserting items into.
		* Replaces random indirect data access with linear direct data access, reduces memory bandwith required.
	* When picking items from assemblers the inserter no longer has to read the assembler itself. Instead it reads from an array that contains the exact data required.
		* Replaces random indirect data access with linear direct data access, reduces memory bandwith required.
3. Labs
	* State and network id of item producing labs is stored in a separate compact `int` array.
		* States
			* Active
			* InactiveOutputFull - Lab can not build any more due to output being full.
			* InactiveInputMissing - Lab is missing items to build with.
		* Reduce computation and memory bandwith required of inserters that can not do anything.
4. Miners
	* Network id that miner siphons power from is stored in separate array.
		* Replaces random indirect data access with linear direct data access, reduces memory bandwith required.
5. Ejectors
	* Network id that ejector siphons power from is stored in separate array.
		* Replaces random indirect data access with linear direct data access, reduces memory bandwith required.
6. Use `[StructLayout(LayoutKind.Auto)]` to reduce size of new structs.
	* Reduces memory bandwith required.
7. Statistics
	* Statistics are not computed for planets that have nothing built on them.
	* Statistics arrays are not cleared on each tick for planets where nothing is built.


# Issues

1. Assembler executor does not have fallback to default execution logic.
2. Can not save right now due to bug
3. Some things are being consumed too fast for some odd reason. Assume it's because of ordering inserters or assemblers. Not sure though.
4. Mod ignores anything created/destroyed on a planet when the player is not on that planet.
5. Some wierd random divide by zero exception in assemblers. Perhaps due to ordering inserters or assemblers. Not sure though.