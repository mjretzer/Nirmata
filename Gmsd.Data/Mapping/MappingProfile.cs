using AutoMapper;
using Gmsd.Data.Dto.Model.Projects;
using Gmsd.Data.Model.Projects;

namespace Gmsd.Data.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Project, ProjectDto>();
        CreateMap<Step, StepDto>();

    }
}
