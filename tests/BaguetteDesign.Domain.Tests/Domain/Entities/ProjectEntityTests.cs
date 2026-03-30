namespace BaguetteDesign.Domain.Tests.Domain.Entities;

using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Domain.Enums;
using Xunit;

public sealed class ProjectEntityTests
{
    [Fact]
    public void IsRevisionLimitReached_WhenAtMax_ReturnsTrue()
    {
        var project = new Project { RevisionCount = 3, MaxRevisions = 3 };
        Assert.True(project.IsRevisionLimitReached);
    }

    [Fact]
    public void IsRevisionLimitReached_WhenAboveMax_ReturnsTrue()
    {
        var project = new Project { RevisionCount = 5, MaxRevisions = 3 };
        Assert.True(project.IsRevisionLimitReached);
    }

    [Fact]
    public void IsRevisionLimitReached_WhenBelowMax_ReturnsFalse()
    {
        var project = new Project { RevisionCount = 2, MaxRevisions = 3 };
        Assert.False(project.IsRevisionLimitReached);
    }

    [Fact]
    public void IsRevisionLimitReached_WhenZeroRevisions_ReturnsFalse()
    {
        var project = new Project { RevisionCount = 0, MaxRevisions = 3 };
        Assert.False(project.IsRevisionLimitReached);
    }

    [Fact]
    public void MaxRevisions_DefaultsTo3()
    {
        var project = new Project();
        Assert.Equal(3, project.MaxRevisions);
    }

    [Fact]
    public void FromLead_CopiesLeadFields()
    {
        var lead = new Lead
        {
            Id = 42,
            UserId = "user-99",
            ServiceType = "logo",
            Brand = "Acme",
            Budget = "$500",
            Deadline = "2 weeks"
        };

        var project = Project.FromLead(lead, maxRevisions: 5);

        Assert.Equal("user-99", project.ClientUserId);
        Assert.Equal(42, project.LeadId);
        Assert.Equal("logo", project.ServiceType);
        Assert.Equal("$500", project.Budget);
        Assert.Equal("2 weeks", project.Deadline);
        Assert.Equal(5, project.MaxRevisions);
    }

    [Fact]
    public void FromLead_WhenBrandIsNull_UsesUserIdInTitle()
    {
        var lead = new Lead { Id = 1, UserId = "u-1", ServiceType = "branding", Brand = null };

        var project = Project.FromLead(lead);

        Assert.Contains("u-1", project.Title);
    }

    [Fact]
    public void Status_DefaultsToActive()
    {
        var project = new Project();
        Assert.Equal(ProjectStatus.Active, project.Status);
    }
}
