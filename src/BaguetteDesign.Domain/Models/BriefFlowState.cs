namespace BaguetteDesign.Domain.Models;

using BaguetteDesign.Domain.Enums;

public sealed record BriefFlowState
{
    public BriefStep CurrentStep { get; init; } = BriefStep.ServiceType;
    public string? ServiceType { get; init; }
    public string? Brand { get; init; }
    public string? Audience { get; init; }
    public string? Style { get; init; }
    public string? Deadline { get; init; }
    public string? Budget { get; init; }
    public string? Country { get; init; }
    public string? AiSummary { get; init; }

    public bool IsCompleted => CurrentStep == BriefStep.Completed;

    public BriefFlowState AdvanceTo(BriefStep step) => this with { CurrentStep = step };

    public BriefFlowState WithAnswer(string answer) => CurrentStep switch
    {
        BriefStep.ServiceType => this with { ServiceType = answer },
        BriefStep.Brand       => this with { Brand = answer },
        BriefStep.Audience    => this with { Audience = answer },
        BriefStep.Style       => this with { Style = answer },
        BriefStep.Deadline    => this with { Deadline = answer },
        BriefStep.Budget      => this with { Budget = answer },
        BriefStep.Country     => this with { Country = answer },
        _                     => this
    };

    public BriefStep NextStep() => CurrentStep switch
    {
        BriefStep.ServiceType => BriefStep.Brand,
        BriefStep.Brand       => BriefStep.Audience,
        BriefStep.Audience    => BriefStep.Style,
        BriefStep.Style       => BriefStep.Deadline,
        BriefStep.Deadline    => BriefStep.Budget,
        BriefStep.Budget      => BriefStep.Country,
        BriefStep.Country     => BriefStep.Summary,
        BriefStep.Summary     => BriefStep.Completed,
        _                     => BriefStep.Completed
    };

    public BriefStep PreviousStep() => CurrentStep switch
    {
        BriefStep.Brand     => BriefStep.ServiceType,
        BriefStep.Audience  => BriefStep.Brand,
        BriefStep.Style     => BriefStep.Audience,
        BriefStep.Deadline  => BriefStep.Style,
        BriefStep.Budget    => BriefStep.Deadline,
        BriefStep.Country   => BriefStep.Budget,
        BriefStep.Summary   => BriefStep.Country,
        _                   => BriefStep.ServiceType
    };
}
