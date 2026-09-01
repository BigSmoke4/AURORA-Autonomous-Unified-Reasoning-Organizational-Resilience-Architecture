using Microsoft.AspNetCore.Identity;

namespace Aurora.Modules.Identity.Domain;

public class ApplicationRole : IdentityRole<Guid>
{
    public string? Description { get; set; }

    public ApplicationRole() { }
    public ApplicationRole(string name, string? description = null) : base(name)
    {
        Description = description;
    }
}
