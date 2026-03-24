using FluentValidation;
using nirmata.Data.Dto.Requests.Projects;

namespace nirmata.Data.Dto.Validators.Projects;

/// <summary>
/// Validator for <see cref="ProjectUpdateRequestDto"/> ensuring business constraint compliance.
/// </summary>
public sealed class ProjectUpdateRequestValidator : AbstractValidator<ProjectUpdateRequestDto>
{
    /// <summary>
    /// Initializes validation rules for project update requests.
    /// </summary>
    public ProjectUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");
    }
}
