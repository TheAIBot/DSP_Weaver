namespace Weaver.Optimizations.LinearDataAccess.Inserters;

internal record struct InserterGrade(int Delay, byte StackInput, byte StackOutput, bool Bidirectional);
