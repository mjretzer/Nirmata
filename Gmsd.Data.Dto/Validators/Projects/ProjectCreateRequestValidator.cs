using FluentValidation;
using Gmsd.Data.Dto.Requests.Projects;

namespace Gmsd.Data.Dto.Validators.Projects;

/// <summary>
/// Validator for <see cref="ProjectCreateRequestDto"/> ensuring business constraint compliance.
/// </summary>
public sealed class ProjectCreateRequestValidator : AbstractValidator<ProjectCreateRequestDto>
{
    /// <summary>
    /// Initializes validation rules for project creation requests.
    /// </summary>
    public ProjectCreateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");
    }
}
