## 1.1.0
* Optimized fractionators.
* Additional assembler data access optimizations.

## 1.0.1
* Fixed sorters on active planets would be updated twice per tick which would sometimes result in a `DivideByZeroException`.
* Fixed that it was not possible to create a new game with the mod loaded.
* Fixed that it was not possible to save the game while on a planet.
* Fixed that any assemblers or labs with no recipe set would cause an `InvalidOperationException` when loading the game.

## 1.0.0
* Initial Release