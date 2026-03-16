using nirmata.Data.Dto.Requests.Projects;
using nirmata.Data.Dto.Validators.Projects;
using Xunit;

namespace nirmata.Data.Dto.Tests.Validators.Projects;

public class ProjectCreateRequestValidatorTests
{
    private readonly ProjectCreateRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        var request = new ProjectCreateRequestDto
        {
            Name = "Valid Project Name"
        };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EmptyName_Fails()
    {
        var request = new ProjectCreateRequestDto
        {
            Name = ""
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_NameExceeds200Characters_Fails()
    {
        var request = new ProjectCreateRequestDto
        {
            Name = new string('a', 201)
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_NameAt200Characters_Passes()
    {
        var request = new ProjectCreateRequestDto
        {
            Name = new string('a', 200)
        };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }
}
