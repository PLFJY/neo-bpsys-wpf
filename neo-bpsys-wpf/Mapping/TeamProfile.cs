using AutoMapper;
using neo_bpsys_wpf.Models;

namespace neo_bpsys_wpf.Mapping
{
    public class TeamProfile : Profile
    {
        public TeamProfile()
        {
            CreateMap<Team, Team>();
        }
    }
}
