namespace BaguetteDesign.Tests;

using BaguetteDesign.Application.Services;
using BaguetteDesign.Domain.Enums;
using BaguetteDesign.Domain.Models;
using Xunit;

public sealed class BriefValidatorTests
{
    private readonly BriefValidator _validator = new();

    [Fact]
    public void Validate_AllRequiredFieldsFilled_IsComplete()
    {
        var state = new BriefFlowState
        {
            ServiceType = "logo",
            Budget = "500 USD",
            Deadline = "2 weeks"
        };

        var result = _validator.Validate(state);

        Assert.True(result.IsComplete);
        Assert.Empty(result.MissingRequiredFields);
    }

    [Fact]
    public void Validate_MissingServiceType_IsIncomplete()
    {
        var state = new BriefFlowState
        {
            Budget = "500 USD",
            Deadline = "2 weeks"
        };

        var result = _validator.Validate(state);

        Assert.False(result.IsComplete);
        Assert.Contains("service_type", result.MissingRequiredFields);
    }

    [Fact]
    public void Validate_MissingBudget_IsIncomplete()
    {
        var state = new BriefFlowState
        {
            ServiceType = "logo",
            Deadline = "2 weeks"
        };

        var result = _validator.Validate(state);

        Assert.False(result.IsComplete);
        Assert.Contains("budget", result.MissingRequiredFields);
    }

    [Fact]
    public void Validate_MissingDeadline_IsIncomplete()
    {
        var state = new BriefFlowState
        {
            ServiceType = "logo",
            Budget = "500 USD"
        };

        var result = _validator.Validate(state);

        Assert.False(result.IsComplete);
        Assert.Contains("deadline", result.MissingRequiredFields);
    }

    [Fact]
    public void Validate_EmptyState_CompletenessScoreIsZero()
    {
        var result = _validator.Validate(new BriefFlowState());

        Assert.Equal(0.0, result.CompletenessScore);
    }

    [Fact]
    public void Validate_AllFieldsFilled_CompletenessScoreIsOne()
    {
        var state = new BriefFlowState
        {
            ServiceType = "logo",
            Brand = "Acme",
            Audience = "Young adults",
            Style = "Minimal",
            Deadline = "2 weeks",
            Budget = "500",
            Country = "Ukraine"
        };

        var result = _validator.Validate(state);

        Assert.Equal(1.0, result.CompletenessScore, precision: 2);
    }

    [Fact]
    public void Validate_HalfFieldsFilled_CompletenessScoreIsHalf()
    {
        // 3 out of 7 optional/required fields filled
        var state = new BriefFlowState
        {
            ServiceType = "logo",
            Brand = "Acme",
            Budget = "500"
        };

        var result = _validator.Validate(state);

        // 3/7 ≈ 0.428
        Assert.True(result.CompletenessScore > 0.4 && result.CompletenessScore < 0.5);
    }
}
