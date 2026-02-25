using AutoMapper; // Import AutoMapper for object-to-object mapping
using Gmsd.Data.Dto.Models.Projects; // Data Transfer Objects for Projects
using Gmsd.Data.Dto.Requests.Projects;
using Gmsd.Data.Entities.Projects; // Domain models for Projects

namespace Gmsd.Data.Mapping; // Mapping layer namespace

public class MappingProfile : Profile // Inherit from AutoMapper.Profile to define configurations
{
    public MappingProfile() // Constructor to register mappings
    {
        CreateMap<Project, ProjectDto>(); // Map Project domain model to ProjectDto
        CreateMap<Step, StepDto>(); // Map Step domain model to StepDto
        CreateMap<Project, ProjectResponseDto>();
        CreateMap<ProjectCreateRequestDto, Project>()
            .ForMember(destination => destination.ProjectId, options => options.Ignore());

    }
}
