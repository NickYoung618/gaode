using Gaode.Domain.Configuration;

namespace Gaode.Infrastructure.Simulation;

public static class ResponsePolicy
{
    public static bool ShouldRespond(SimStage stage) => stage.Strategy != "NoResponse";
    public static bool IsFailure(SimStage stage) => stage.Strategy == "Fail" || stage.Outcome == "Failure";
    public static bool IsHold(SimStage stage) => stage.Outcome == "Hold";
    public static string? FailureCode(SimStage stage) => IsFailure(stage) ? stage.FailureCode ?? "SimulatedFailure" : null;
}
