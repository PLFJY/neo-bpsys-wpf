using neo_bpsys.Core.Enums;

namespace neo_bpsys.Core.Models;

public class Character
{
    public Character(string name, Camp camp, string imageFileName)
    {
        Name = name;
        Camp = camp;
        ImageFileName = imageFileName;
    }

    public string Name { get; }
    public Camp Camp { get; }
    public string ImageFileName { get; }
}
