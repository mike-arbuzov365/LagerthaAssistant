namespace BaguetteDesign.Tests;

using BaguetteDesign.Domain.Enums;
using BaguetteDesign.Domain.Models;
using Xunit;

public sealed class BriefFlowStateTests
{
    [Fact]
    public void WithAnswer_ServiceTypeStep_SetsServiceType()
    {
        var state = new BriefFlowState { CurrentStep = BriefStep.ServiceType };
        var updated = state.WithAnswer("logo");
        Assert.Equal("logo", updated.ServiceType);
    }

    [Fact]
    public void WithAnswer_BudgetStep_SetsBudget()
    {
        var state = new BriefFlowState { CurrentStep = BriefStep.Budget };
        var updated = state.WithAnswer("500 USD");
        Assert.Equal("500 USD", updated.Budget);
    }

    [Fact]
    public void NextStep_ServiceType_ReturnsBrand()
    {
        var state = new BriefFlowState { CurrentStep = BriefStep.ServiceType };
        Assert.Equal(BriefStep.Brand, state.NextStep());
    }

    [Fact]
    public void NextStep_Country_ReturnsSummary()
    {
        var state = new BriefFlowState { CurrentStep = BriefStep.Country };
        Assert.Equal(BriefStep.Summary, state.NextStep());
    }

    [Fact]
    public void NextStep_Summary_ReturnsCompleted()
    {
        var state = new BriefFlowState { CurrentStep = BriefStep.Summary };
        Assert.Equal(BriefStep.Completed, state.NextStep());
    }

    [Fact]
    public void PreviousStep_Budget_ReturnsDeadline()
    {
        var state = new BriefFlowState { CurrentStep = BriefStep.Budget };
        Assert.Equal(BriefStep.Deadline, state.PreviousStep());
    }

    [Fact]
    public void IsCompleted_WhenStepIsCompleted_ReturnsTrue()
    {
        var state = new BriefFlowState { CurrentStep = BriefStep.Completed };
        Assert.True(state.IsCompleted);
    }

    [Fact]
    public void IsCompleted_WhenStepIsNotCompleted_ReturnsFalse()
    {
        var state = new BriefFlowState { CurrentStep = BriefStep.Brand };
        Assert.False(state.IsCompleted);
    }

    [Fact]
    public void AdvanceTo_ChangesCurrentStep()
    {
        var state = new BriefFlowState { CurrentStep = BriefStep.ServiceType };
        var advanced = state.AdvanceTo(BriefStep.Budget);
        Assert.Equal(BriefStep.Budget, advanced.CurrentStep);
    }

    [Fact]
    public void WithAnswer_DoesNotMutateOriginal()
    {
        var original = new BriefFlowState { CurrentStep = BriefStep.ServiceType };
        var updated = original.WithAnswer("logo");
        Assert.Null(original.ServiceType);
        Assert.Equal("logo", updated.ServiceType);
    }
}
