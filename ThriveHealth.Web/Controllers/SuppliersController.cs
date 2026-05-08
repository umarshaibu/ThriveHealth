using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Inventory;
using ThriveHealth.Web.Models.ViewModels;

namespace ThriveHealth.Web.Controllers;

[HasPermission(Permissions.PurchaseOrderManage)]
public class SuppliersController : Controller
{
    public const string ProcurementStaff = Roles.ProcurementOfficer + "," + Roles.StoreOfficer + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector + "," +
        Roles.ChiefFinancialOfficer + "," + Roles.Accountant;

    private readonly ApplicationDbContext _db;
    public SuppliersController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var suppliers = await _db.Suppliers.AsNoTracking()
            .OrderBy(s => s.Name).ToListAsync();
        return View(suppliers);
    }

    [HttpGet]
    public IActionResult Create() => View(new SupplierEditViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SupplierEditViewModel m)
    {
        if (!ModelState.IsValid) return View(m);
        _db.Suppliers.Add(new Supplier
        {
            Name = m.Name, Code = m.Code.ToUpperInvariant(),
            ContactPerson = m.ContactPerson, Phone = m.Phone, Email = m.Email,
            Address = m.Address, TaxId = m.TaxId, RcNumber = m.RcNumber,
            BankName = m.BankName, BankAccountNumber = m.BankAccountNumber,
            PaymentTerms = m.PaymentTerms, LeadTimeDays = m.LeadTimeDays,
            IsActive = m.IsActive
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Supplier '{m.Name}' created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var s = await _db.Suppliers.FindAsync(id);
        if (s is null) return NotFound();
        return View(new SupplierEditViewModel
        {
            Id = s.Id, Name = s.Name, Code = s.Code,
            ContactPerson = s.ContactPerson, Phone = s.Phone, Email = s.Email,
            Address = s.Address, TaxId = s.TaxId, RcNumber = s.RcNumber,
            BankName = s.BankName, BankAccountNumber = s.BankAccountNumber,
            PaymentTerms = s.PaymentTerms, LeadTimeDays = s.LeadTimeDays,
            IsActive = s.IsActive
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(SupplierEditViewModel m)
    {
        var s = await _db.Suppliers.FindAsync(m.Id);
        if (s is null) return NotFound();
        if (!ModelState.IsValid) return View(m);

        s.Name = m.Name; s.Code = m.Code.ToUpperInvariant();
        s.ContactPerson = m.ContactPerson; s.Phone = m.Phone; s.Email = m.Email;
        s.Address = m.Address; s.TaxId = m.TaxId; s.RcNumber = m.RcNumber;
        s.BankName = m.BankName; s.BankAccountNumber = m.BankAccountNumber;
        s.PaymentTerms = m.PaymentTerms; s.LeadTimeDays = m.LeadTimeDays;
        s.IsActive = m.IsActive;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Supplier updated.";
        return RedirectToAction(nameof(Index));
    }
}
