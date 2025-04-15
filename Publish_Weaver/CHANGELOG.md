## 1,1,1
* Fix optimized power system in single threaded mode.

## 1.1.0
* ~15% performance improvement compared to 1.0.1
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