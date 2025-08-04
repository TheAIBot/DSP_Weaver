namespace Weaver.Optimizations.Inserters;

internal record struct InserterGrade(int Delay, byte StackInput, byte StackOutput, bool Bidirectional);
