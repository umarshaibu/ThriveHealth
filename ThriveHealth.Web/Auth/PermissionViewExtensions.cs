using Microsoft.AspNetCore.Razor.TagHelpers;
using ThriveHealth.Web.Services;

namespace ThriveHealth.Web.Auth;

/// <summary>
/// Tag helper to gate UI elements by permission.
/// Usage: <code>&lt;div asp-permission="patients.register"&gt;...&lt;/div&gt;</code>
/// If the current user lacks the permission the entire element is suppressed.
/// </summary>
[HtmlTargetElement(Attributes = "asp-permission")]
public sealed class PermissionTagHelper : TagHelper
{
    private readonly IPermissionService _perms;
    private readonly IHttpContextAccessor _http;

    public PermissionTagHelper(IPermissionService perms, IHttpContextAccessor http)
    {
        _perms = perms; _http = http;
    }

    [HtmlAttributeName("asp-permission")]
    public string? Permission { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (string.IsNullOrWhiteSpace(Permission)) return;
        var user = _http.HttpContext?.User;
        if (user is null || user.Identity?.IsAuthenticated != true) { output.SuppressOutput(); return; }
        if (!await _perms.UserHasAsync(user, Permission!)) output.SuppressOutput();
    }
}
