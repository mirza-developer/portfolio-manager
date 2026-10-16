using Microsoft.AspNetCore.Identity;

namespace PortfolioManager.Data.Identity;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
