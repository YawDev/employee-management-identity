using AutoMapper;
using employee.management.identity.Contracts.Request;
using employee.management.identity.models.Dtos;

namespace employee.management.identity.Mapping
{
    public class MapperProfile : Profile
    {
        public MapperProfile()
        {
            CreateMap<RegisterRequest, CreateIdentityDTO>()
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.UserName.Trim()))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email.ToLower()));
        }
    }
}
