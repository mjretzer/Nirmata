using AutoMapper; // Import AutoMapper for object-to-object mapping
using nirmata.Data.Dto.Models.Projects; // Data Transfer Objects for Projects
using nirmata.Data.Dto.Requests.Projects;
using nirmata.Data.Entities.Projects; // Domain models for Projects

namespace nirmata.Data.Mapping; // Mapping layer namespace

public class MappingProfile : Profile // Inherit from AutoMapper.Profile to define configurations
{
    public MappingProfile() // Constructor to register mappings
    {
        CreateMap<Project, ProjectDto>(); // Map Project domain model to ProjectDto
        CreateMap<Step, StepDto>(); // Map Step domain model to StepDto
        CreateMap<Project, ProjectResponseDto>();
        CreateMap<ProjectCreateRequestDto, Project>()
            .ForMember(destination => destination.ProjectId, options => options.Ignore());
        CreateMap<ProjectUpdateRequestDto, Project>()
            .ForMember(destination => destination.ProjectId, options => options.Ignore())
            .ForMember(destination => destination.Steps, options => options.Ignore());
    }
}
