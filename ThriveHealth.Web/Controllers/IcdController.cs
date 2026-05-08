using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThriveHealth.Web.Services;

namespace ThriveHealth.Web.Controllers;

[Authorize]
public class IcdController : Controller
{
    private readonly IIcdSearchService _icd;
    public IcdController(IIcdSearchService icd) => _icd = icd;

    [HttpGet]
    public async Task<IActionResult> Search(string? q)
    {
        var hits = await _icd.SearchAsync(q ?? string.Empty);
        return Json(hits.Select(h => new { code = h.Code, description = h.Description, category = h.Category }));
    }
}
