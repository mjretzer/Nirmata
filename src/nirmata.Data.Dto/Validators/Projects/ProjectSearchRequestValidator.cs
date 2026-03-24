using FluentValidation;
using nirmata.Data.Dto.Requests.Projects;

namespace nirmata.Data.Dto.Validators.Projects;

/// <summary>
/// Validator for <see cref="ProjectSearchRequestDto"/> ensuring valid pagination and filter parameters.
/// </summary>
public sealed class ProjectSearchRequestValidator : AbstractValidator<ProjectSearchRequestDto>
{
    /// <summary>
    /// Initializes validation rules for project search requests.
    /// </summary>
    public ProjectSearchRequestValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");

        When(x => x.SearchTerm is not null, () =>
        {
            RuleFor(x => x.SearchTerm)
                .MaximumLength(200).WithMessage("Search term must not exceed 200 characters.");
        });
    }
}
