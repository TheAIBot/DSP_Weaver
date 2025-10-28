using System.Collections.Generic;
using System.Linq;
using Weaver.FatoryGraphs;
using Weaver.Optimizations.NeedsSystem;
using Weaver.Optimizations.PowerSystems;
using Weaver.Optimizations.Statistics;

namespace Weaver.Optimizations.Assemblers;

internal sealed class AssemblerExecutor
{
    public NetworkIdAndState<AssemblerState>[] _assemblerNetworkIdAndStates = null!;
    public OptimizedAssembler[] _optimizedAssemblers = null!;
    private bool[] _assemblerReplicatings = null!;
    private int[] _assemblerExtraPowerRatios = null!;
    private AssemblerTimingData[] _assemblersTimingData = null!;
    public short[] _assemblerRecipeIndexes = null!;
    public AssemblerRecipe[] _assemblerRecipes = null!;
    public Dictionary<int, int> _assemblerIdToOptimizedIndex = null!;
    public HashSet<int> _unOptimizedAssemblerIds = null!;
    private PrototypePowerConsumptionExecutor _prototypePowerConsumptionExecutor;

    public int _producedSize = -1;
    public short[] _served = null!;
    public short[] _incServed = null!;
    public short[] _produced = null!;


    public int Count => _optimizedAssemblers.Length;

    public void GameTick(PlanetFactory planet,
                         int[] assemblerPowerConsumerTypeIndexes,
                         PowerConsumerType[] powerConsumerTypes,
                         long[] networksPowerConsumption,
                         int[] productRegister,
                         int[] consumeRegister,
                         SubFactoryNeeds subFactoryNeeds)
    {
        PowerSystem powerSystem = planet.powerSystem;
        float[] networkServes = powerSystem.networkServes;
        OptimizedAssembler[] optimizedAssemblers = _optimizedAssemblers;
        AssemblerRecipe[] assemblerRecipes = _assemblerRecipes;
        bool[] assemblerReplicatings = _assemblerReplicatings;
        int[] assemblerExtraPowerRatios = _assemblerExtraPowerRatios;
        AssemblerTimingData[] assemblersTimingData = _assemblersTimingData;
        GroupNeeds groupNeeds = subFactoryNeeds.GetGroupNeeds(EntityType.Assembler);
        short[] needs = subFactoryNeeds.Needs;
        int producedSize = _producedSize;
        short[] served = _served;
        short[] incServed = _incServed;
        short[] produced = _produced;
        short[] assemblerRecipeIndexes = _assemblerRecipeIndexes;

        for (int assemblerIndex = 0; assemblerIndex < optimizedAssemblers.Length; assemblerIndex++)
        {
            ref NetworkIdAndState<AssemblerState> assemblerNetworkIdAndState = ref _assemblerNetworkIdAndStates[assemblerIndex];
            ref bool replicating = ref assemblerReplicatings[assemblerIndex];
            ref int extraPowerRatios = ref assemblerExtraPowerRatios[assemblerIndex];
            if ((AssemblerState)assemblerNetworkIdAndState.State != AssemblerState.Active)
            {
                UpdatePower(assemblerPowerConsumerTypeIndexes, powerConsumerTypes, networksPowerConsumption, assemblerIndex, assemblerNetworkIdAndState.Index, replicating, extraPowerRatios);
                continue;
            }

            ref readonly AssemblerRecipe recipeData = ref assemblerRecipes[assemblerRecipeIndexes[assemblerIndex]];
            ref AssemblerTimingData assemblerTimingData = ref assemblersTimingData[assemblerIndex];
            OptimizedAssembler.UpdateNeeds(in recipeData,
                                           ref assemblerTimingData,
                                           groupNeeds,
                                           served,
                                           needs,
                                           assemblerIndex);
            float power = networkServes[assemblerNetworkIdAndState.Index];
            if (!assemblerTimingData.UpdateTimings(power, replicating, in recipeData))
            {
                UpdatePower(assemblerPowerConsumerTypeIndexes, powerConsumerTypes, networksPowerConsumption, assemblerIndex, assemblerNetworkIdAndState.Index, replicating, extraPowerRatios);
                continue;
            }

            int servedOffset = assemblerIndex * groupNeeds.GroupNeedsSize;
            int producedOffset = assemblerIndex * producedSize;
            ref OptimizedAssembler assembler = ref optimizedAssemblers[assemblerIndex];
            assemblerNetworkIdAndState.State = (int)assembler.Update(power,
                                                                     productRegister,
                                                                     consumeRegister,
                                                                     in recipeData,
                                                                     ref replicating,
                                                                     ref extraPowerRatios,
                                                                     ref assemblerTimingData,
                                                                     servedOffset,
                                                                     producedOffset,
                                                                     served,
                                                                     incServed,
                                                                     produced);

            UpdatePower(assemblerPowerConsumerTypeIndexes, powerConsumerTypes, networksPowerConsumption, assemblerIndex, assemblerNetworkIdAndState.Index, replicating, extraPowerRatios);
        }
    }

    public void UpdatePower(int[] assemblerPowerConsumerTypeIndexes,
                            PowerConsumerType[] powerConsumerTypes,
                            long[] networksPowerConsumption)
    {
        OptimizedAssembler[] optimizedAssemblers = _optimizedAssemblers;
        NetworkIdAndState<AssemblerState>[] assemblerNetworkIdAndStates = _assemblerNetworkIdAndStates;
        bool[] assemblerReplicatings = _assemblerReplicatings;
        int[] assemblerExtraPowerRatios = _assemblerExtraPowerRatios;
        for (int assemblerIndex = 0; assemblerIndex < optimizedAssemblers.Length; assemblerIndex++)
        {
            int networkIndex = assemblerNetworkIdAndStates[assemblerIndex].Index;
            bool replicating = assemblerReplicatings[assemblerIndex];
            int extraPowerRatios = assemblerExtraPowerRatios[assemblerIndex];
            UpdatePower(assemblerPowerConsumerTypeIndexes, powerConsumerTypes, networksPowerConsumption, assemblerIndex, networkIndex, replicating, extraPowerRatios);
        }
    }

    private static void UpdatePower(int[] assemblerPowerConsumerTypeIndexes,
                                    PowerConsumerType[] powerConsumerTypes,
                                    long[] networksPowerConsumption,
                                    int assemblerIndex,
                                    int networkIndex,
                                    bool replicating,
                                    int extraPowerRatios)
    {
        int powerConsumerTypeIndex = assemblerPowerConsumerTypeIndexes[assemblerIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        networksPowerConsumption[networkIndex] += GetPowerConsumption(powerConsumerType, replicating, extraPowerRatios);
    }

    public PrototypePowerConsumptions UpdatePowerConsumptionPerPrototype(int[] assemblerPowerConsumerTypeIndexes,
                                                                         PowerConsumerType[] powerConsumerTypes)
    {
        var prototypePowerConsumptionExecutor = _prototypePowerConsumptionExecutor;
        prototypePowerConsumptionExecutor.Clear();

        bool[] assemblerReplicatings = _assemblerReplicatings;
        int[] assemblerExtraPowerRatios = _assemblerExtraPowerRatios;
        int[] prototypeIdIndexes = prototypePowerConsumptionExecutor.PrototypeIdIndexes;
        long[] prototypeIdPowerConsumption = prototypePowerConsumptionExecutor.PrototypeIdPowerConsumption;
        for (int assemblerIndex = 0; assemblerIndex < assemblerReplicatings.Length; assemblerIndex++)
        {
            bool replicating = assemblerReplicatings[assemblerIndex];
            int extraPowerRatio = assemblerExtraPowerRatios[assemblerIndex];
            UpdatePowerConsumptionPerPrototype(assemblerPowerConsumerTypeIndexes,
                                               powerConsumerTypes,
                                               prototypeIdIndexes,
                                               prototypeIdPowerConsumption,
                                               assemblerIndex,
                                               replicating,
                                               extraPowerRatio);
        }

        return prototypePowerConsumptionExecutor.GetPowerConsumption();
    }

    private static void UpdatePowerConsumptionPerPrototype(int[] assemblerPowerConsumerTypeIndexes,
                                                           PowerConsumerType[] powerConsumerTypes,
                                                           int[] prototypeIdIndexes,
                                                           long[] prototypeIdPowerConsumption,
                                                           int assemblerIndex,
                                                           bool replicating,
                                                           int extraPowerRatio)
    {
        int powerConsumerTypeIndex = assemblerPowerConsumerTypeIndexes[assemblerIndex];
        PowerConsumerType powerConsumerType = powerConsumerTypes[powerConsumerTypeIndex];
        prototypeIdPowerConsumption[prototypeIdIndexes[assemblerIndex]] += GetPowerConsumption(powerConsumerType, replicating, extraPowerRatio);
    }

    public void Save(PlanetFactory planet, SubFactoryNeeds subFactoryNeeds)
    {
        AssemblerComponent[] assemblers = planet.factorySystem.assemblerPool;
        OptimizedAssembler[] optimizedAssemblers = _optimizedAssemblers;
        bool[] assemblerReplicatings = _assemblerReplicatings;
        int[] assemblerExtraPowerRatios = _assemblerExtraPowerRatios;
        AssemblerTimingData[] assemblersTimingData = _assemblersTimingData;
        GroupNeeds groupNeeds = subFactoryNeeds.GetGroupNeeds(EntityType.Assembler);
        short[] needs = subFactoryNeeds.Needs;
        int producedSize = _producedSize;
        short[] served = _served;
        short[] incServed = _incServed;
        short[] produced = _produced;
        for (int i = 1; i < planet.factorySystem.assemblerCursor; i++)
        {
            if (!_assemblerIdToOptimizedIndex.TryGetValue(i, out int optimizedIndex))
            {
                continue;
            }

            ref readonly OptimizedAssembler optimizedAssembler = ref optimizedAssemblers[optimizedIndex];
            optimizedAssembler.Save(ref assemblers[i],
                                    assemblerReplicatings[optimizedIndex],
                                    assemblerExtraPowerRatios[optimizedIndex],
                                    in assemblersTimingData[optimizedIndex],
                                    groupNeeds,
                                    needs,
                                    producedSize,
                                    served,
                                    incServed,
                                    produced,
                                    optimizedIndex);
        }
    }

    public void InitializeAssemblers(PlanetFactory planet,
                                     Graph subFactoryGraph,
                                     SubFactoryPowerSystemBuilder subFactoryPowerSystemBuilder,
                                     SubFactoryProductionRegisterBuilder subFactoryProductionRegisterBuilder,
                                     SubFactoryNeedsBuilder subFactoryNeedsBuilder)
    {
        List<NetworkIdAndState<AssemblerState>> assemblerNetworkIdAndStates = [];
        List<OptimizedAssembler> optimizedAssemblers = [];
        List<bool> assemblerReplicatings = [];
        List<int> assemblerExtraPowerRatios = [];
        List<AssemblerTimingData> assemblersTimingData = [];
        List<short> assemblerRecipeIndexes = [];
        Dictionary<AssemblerRecipe, int> assemblerRecipeToIndex = [];
        List<AssemblerRecipe> assemblerRecipes = [];
        Dictionary<int, int> assemblerIdToOptimizedIndex = [];
        HashSet<int> unOptimizedAssemblerIds = [];
        List<int[]> served = [];
        List<int[]> incServed = [];
        List<int[]> produced = [];
        var prototypePowerConsumptionBuilder = new PrototypePowerConsumptionBuilder();
        GroupNeedsBuilder needsBuilder = subFactoryNeedsBuilder.CreateGroupNeedsBuilder(EntityType.Assembler);
        GameHistoryData historyData = planet.gameData.history;
        foreach (int assemblerIndex in subFactoryGraph.GetAllNodes()
                                                      .Where(x => x.EntityTypeIndex.EntityType == EntityType.Assembler)
                                                      .Select(x => x.EntityTypeIndex.Index)
                                                      .OrderBy(x => x))
        {
            ref AssemblerComponent assembler = ref planet.factorySystem.assemblerPool[assemblerIndex];
            if (assembler.id != assemblerIndex)
            {
                unOptimizedAssemblerIds.Add(assemblerIndex);
                continue;
            }

            if (assembler.recipeId == 0)
            {
                unOptimizedAssemblerIds.Add(assemblerIndex);
                continue;
            }

            // An assembler where no recipe has ever been set will somehow have recipeId != 0
            // while needs == null
            if (assembler.needs == null)
            {
                unOptimizedAssemblerIds.Add(assemblerIndex);
                continue;
            }

            // It is possible to put a locked recipe into a building by using blueprints.
            // Such buildings should not run at all.
            // Planet reoptimization will enable the recipe when it has been researched.
            if (!historyData.RecipeUnlocked(assembler.recipeId))
            {
                unOptimizedAssemblerIds.Add(assemblerIndex);
                continue;
            }

            AssemblerRecipe assemblerRecipe = new AssemblerRecipe(assembler.recipeId,
                                                                  assembler.recipeType,
                                                                  assembler.timeSpend,
                                                                  assembler.extraTimeSpend,
                                                                  assembler.productive,
                                                                  subFactoryProductionRegisterBuilder.AddConsume(assembler.requires),
                                                                  assembler.requireCounts,
                                                                  subFactoryProductionRegisterBuilder.AddProduct(assembler.products),
                                                                  assembler.productCounts);
            if (!assemblerRecipeToIndex.TryGetValue(assemblerRecipe, out int assemblerRecipeIndex))
            {
                assemblerRecipeIndex = assemblerRecipeToIndex.Count;
                assemblerRecipeToIndex.Add(assemblerRecipe, assemblerRecipeIndex);
                assemblerRecipes.Add(assemblerRecipe);
            }

            assemblerIdToOptimizedIndex.Add(assembler.id, optimizedAssemblers.Count);
            int networkIndex = planet.powerSystem.consumerPool[assembler.pcId].networkId;
            assemblerNetworkIdAndStates.Add(new NetworkIdAndState<AssemblerState>((int)(assembler.recipeId == 0 ? AssemblerState.InactiveNoRecipeSet : AssemblerState.Active),
                                                                                  networkIndex));
            optimizedAssemblers.Add(new OptimizedAssembler(ref assembler));
            assemblerReplicatings.Add(assembler.replicating);
            assemblerExtraPowerRatios.Add(assembler.extraPowerRatio);
            assemblersTimingData.Add(new AssemblerTimingData(in assembler));
            served.Add(assembler.served);
            incServed.Add(assembler.incServed);
            produced.Add(assembler.produced);
            assemblerRecipeIndexes.Add((short)assemblerRecipeIndex);
            subFactoryPowerSystemBuilder.AddAssembler(ref assembler, networkIndex);
            prototypePowerConsumptionBuilder.AddPowerConsumer(in planet.entityPool[assembler.entityId]);


            // set it here so we don't have to set it in the update loop.
            planet.entityNeeds[assembler.entityId] = assembler.needs;
            needsBuilder.AddNeeds(assembler.needs, assemblerRecipe.Requires.Length);
        }

        if (assemblerRecipes.Count > 0)
        {
            int maxServedsSize = assemblerRecipes.Max(x => x.Requires.Length);
            int maxProducedSize = assemblerRecipes.Max(x => x.Products.Length);
            List<short> servedFlat = [];
            List<short> incServedFlat = [];
            List<short> producedFlat = [];

            // Apparently some assemblers have a ridiculously high number of items inside it.
            // I assume this to be a bug an clamp it to -5000, 5000 because i don't expect any
            // assembler recipe would need such a high buffer for anything.
            const int minAllowedValue = -5000;
            const int maxAllowedValue = 5000;

            for (int assemblerIndex = 0; assemblerIndex < optimizedAssemblers.Count; assemblerIndex++)
            {
                for (int servedIndex = 0; servedIndex < maxServedsSize; servedIndex++)
                {
                    servedFlat.Add(GroupNeeds.GetOrDefaultConvertToShortWithClamping(served[assemblerIndex], servedIndex, minAllowedValue, maxAllowedValue));
                    incServedFlat.Add(GroupNeeds.GetOrDefaultConvertToShortWithClamping(incServed[assemblerIndex], servedIndex, minAllowedValue, maxAllowedValue));
                }

                for (int producedIndex = 0; producedIndex < maxProducedSize; producedIndex++)
                {
                    producedFlat.Add(GroupNeeds.GetOrDefaultConvertToShortWithClamping(produced[assemblerIndex], producedIndex, minAllowedValue, maxAllowedValue));
                }
            }

            _producedSize = maxProducedSize;
            _served = servedFlat.ToArray();
            _incServed = incServedFlat.ToArray();
            _produced = producedFlat.ToArray();
        }

        _assemblerNetworkIdAndStates = assemblerNetworkIdAndStates.ToArray();
        _assemblerRecipes = assemblerRecipes.ToArray();
        _optimizedAssemblers = optimizedAssemblers.ToArray();
        _assemblerReplicatings = assemblerReplicatings.ToArray();
        _assemblerExtraPowerRatios = assemblerExtraPowerRatios.ToArray();
        _assemblersTimingData = assemblersTimingData.ToArray();
        _assemblerRecipeIndexes = assemblerRecipeIndexes.ToArray();
        _assemblerIdToOptimizedIndex = assemblerIdToOptimizedIndex;
        _unOptimizedAssemblerIds = unOptimizedAssemblerIds;
        _prototypePowerConsumptionExecutor = prototypePowerConsumptionBuilder.Build();
        needsBuilder.Complete();
    }

    private static long GetPowerConsumption(PowerConsumerType powerConsumerType, bool assemblerReplicating, int assemblerExtraPowerRatio)
    {
        return powerConsumerType.GetRequiredEnergy(assemblerReplicating, 1000 + assemblerExtraPowerRatio);
    }
}
