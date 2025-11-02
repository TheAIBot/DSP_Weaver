## 2.1.0
* 5-10% DSP performance improvement compared to 2.0.4
* Optimized sorters memory usage.
* Optimized belts memory usage and accessing belts.
* Optimized dyson sphere power calculation.
* Deduplicate "static data" across star cluster.
* Optimize assembler needs update.
* Fix ray receivers dyson power demand was updated twice.
* Fix sorters on optimized planets could not take items from silos and ejectors and put them on belts.
* Fix turrets would not shoot down incomming relays while player was off planet.
* Popup box will now report if any used mods are known to be incompatible with Weaver.
	* Added configuration WarnAboutModIncompatibility to disable the popup box.

## 2.0.4
* Updated to DSP version 0.10.33.27005.
* Fix going to a planet with a recently destroyed assembler could cause the game to crash.

## 2.0.3
* 0-10% DSP performance improvement compared to 2.0.2
* Fix Weavers parallel simulation could deadlock itself.

## 2.0.2
* Fix Weavers parallel simulation could deadlock itself.
* Fix Weaver did not support running on a single thread.

## 2.0.1
* Fix crash when player attempted to build with personal drones.

## 2.0.0
* Updated to DSP version 0.10.33.26943.
* 0-10% DSP performance improvement compared to 1.5.1
* Replaced DSP multithreading with Weavers existing multithreading implementation.
	* Temporarily made dyson swarm and dyson rocket update sequential.
	* Temporarily reduced parallelism of power consumtion, power generation, assembler, sorter, for local planet only, to one thread per planet.
	* Added dark fog, dyson sphere power generation, construction system, defense turret to weavers mulithreading.
* Optimized dyson sphere power production calculation.

## 1.5.1
* Fix sorter taking from storage to assembler would ignore assembler item limit and put items into assembler forever.
* Fix advanced mining machine belts input/output would not work when the player left the planet.

## 1.5.0
* 10-20% DSP performance improvement compared to 1.4.2
* Improved data access patterns.
	* Flattened arrays used by assemblers and labs to improve sequential data access.
	* Ordered sorter update order to improve sequential data access.
	* Improve belts item data access by storing belt items directly in the belts data.
* Fixed using sorters to move fuel between power generators did not work.

## 1.4.2
* Fix fractionator consumption of fluid was added to production statistics instead of consumptions statistics.

## 1.4.1
* Fix gas planet production statistics.

## 1.4.0
* 5-20% DSP performance improvement compared to 1.3.0
* Optimized production statistics.
	* Fix lag spikes caused by statistics calculations.
	* Removed locks on production statistics.
	* Traffic and combat statistics only run on planets when they actually have any traffic or combat.
* Fix accessing belts when other mods do not remove them in the same way the games does it.

## 1.3.0
* 3-10% DSP performance improvement compared to 1.2.6
* Power system should no longer have any performance impact.
	* Calculation of power consumption is now done in an entitys regular update loop.
	* Wind power generation update is now a constant time operation i.e. it will never have a performance impact, no matter how many wind generators you have.
	* Power system calculation of transmission tower power usage is now a constant time operation. Any number of tesla towers, Satellite substations etc will not affect performance at all.
	* Updating ray receivers view of a dyson sphere has been parallelized.
* Fix real time power consumption statistics was not updating.
* Fix power consumption calculation did not include power transmission towers.
* Fix power consumption calculation was not correct due to sub-factories changing power consumer indexes.

## 1.2.6
* Fix some sorters were not assigned the correct sorter grade.
* Fix sorters could not interact with the first building of each entity type.

## 1.2.5
* Fix sorters were incorrectly interacting with very long belts.
* Fix labs did not update their display icon when research changed.

## 1.2.4
* Fix optimized stacked tanks would sometimes crash the game.
* Fix Labs could, in rare cases, end up not consuming matrices.
* Fix Arriving at a planet doing research would crash the game if a research completed while player was on the planet.

## 1.2.3
* Fix mining machine would not output to belt when player was off planet.
* Fix sorters pick/place position for belts was slightly incorrect in some cases.

## 1.2.2
* Fix newly placed orbital collectors would not work while player is away from gas giant until game has been saved and loaded again.

## 1.2.1
* Fix items moving vertically in stacked labs would briefly clog causing a 1-2% production reduction.

## 1.2.0
* 10-15% DSP performance improvement compared to 1.1.5
* Improved multithreading.
* Optimized multiple entity types: Ejectors, belts, all types of miners, belt monitors, pilers, splitters, stations, tanks, turrets, ray receivers and power exchangers.
* Fix spray coaters did not consume power.

## 1.1.5
* Fix sorter inserting into assembler or lab without a recipe set would crash the game.

## 1.1.4
* Fix sorters could not insert into silos after loading save. Player needed to visit silos planets before sorters would function again.

## 1.1.3
* Fix researching tech on unoptimized planet would crash the game.

## 1.1.2
* Fix researching tech would sometimes immediately crash the game due to UI code not being executed on the main thread.
* Fix Weaver did not care if a recipe was unlocked or not.

## 1.1.1
* Fix optimized power system in single threaded mode.

## 1.1.0
* ~15% DSP performance improvement compared to 1.0.1
* Optimized fractionators.
* Additional assembler data access optimizations.
* Optimized multithreaded work distribution method.
	* Parallelized station belt logic, splitters, monitors, pilers, storage and the digital system.
	* Smoothes out most lag spikes.
* Fix import/export statistics were not updated correctly.
* Fix some inserters would become inactive when the player was on a planet.
* First stab at supporting SampleAndHoldSim. Loads but statistics are broken.

## 1.0.1
* Fixed sorters on active planets would be updated twice per tick which would sometimes result in a `DivideByZeroException`.
* Fixed that it was not possible to create a new game with the mod loaded.
* Fixed that it was not possible to save the game while on a planet.
* Fixed that any assemblers or labs with no recipe set would cause an `InvalidOperationException` when loading the game.

## 1.0.0
* Initial Release