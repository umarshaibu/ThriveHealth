using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Diagnostics;
using ThriveHealth.Web.Models.Emergency;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Hr;
using ThriveHealth.Web.Models.Immunization;
using ThriveHealth.Web.Models.Reporting;
using ThriveHealth.Web.Models.Inpatient;
using ThriveHealth.Web.Models.Insurance;
using ThriveHealth.Web.Models.Inventory;
using ThriveHealth.Web.Models.Theatre;
using ThriveHealth.Web.Models.Pharmacy;
using ThriveHealth.Web.Models.Scheduling;

namespace ThriveHealth.Web.Data;

public static class DbSeeder
{
    /// <summary>
    /// Unified password for every seeded demo user. Picked so it satisfies Identity's default
    /// requirements (length 8+, upper, lower, digit) without trying to be secret — this is
    /// dev/demo data only. Override via <c>Seed:DemoPassword</c> in appsettings if you ever
    /// want to harden a shared demo environment.
    /// </summary>
    public const string DemoPassword = "Demo@12345";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var db = services.GetRequiredService<ApplicationDbContext>();
        await SeedDefaultRolePermissionsAsync(db, roleManager);
        var config = services.GetRequiredService<IConfiguration>();

        var facCfg = config.GetSection("Seed:Facility");
        var code = facCfg["Code"] ?? "TH-DEMO";
        var facility = await db.Facilities.FirstOrDefaultAsync(f => f.Code == code);
        if (facility is null)
        {
            facility = new Facility
            {
                Name = facCfg["Name"] ?? "ThriveHealth Demo Hospital",
                Code = code,
                Tier = (FacilityTier)int.Parse(facCfg["Tier"] ?? "2"),
                Type = (FacilityType)int.Parse(facCfg["Type"] ?? "3"),
                Address = facCfg["Address"],
                Lga = facCfg["Lga"],
                State = facCfg["State"],
                Phone = facCfg["Phone"],
                Email = facCfg["Email"],
                BedCapacity = int.Parse(facCfg["BedCapacity"] ?? "0"),
                RegistrationNumber = facCfg["RegistrationNumber"],
                HospitalNumberPrefix = facCfg["HospitalNumberPrefix"]
            };
            db.Facilities.Add(facility);
            await db.SaveChangesAsync();
        }

        if (!await db.Clinics.AnyAsync(c => c.FacilityId == facility.Id))
        {
            db.Clinics.AddRange(
                new Clinic { FacilityId = facility.Id, Name = "General OPD", Code = "OPD", Specialty = ClinicSpecialty.GeneralOpd, ColorHex = "#1f6feb", DefaultSlotMinutes = 15 },
                new Clinic { FacilityId = facility.Id, Name = "Antenatal Clinic", Code = "ANC", Specialty = ClinicSpecialty.Antenatal, ColorHex = "#c81e1e", DefaultSlotMinutes = 20 },
                new Clinic { FacilityId = facility.Id, Name = "Paediatrics", Code = "PAED", Specialty = ClinicSpecialty.Paediatrics, ColorHex = "#1f9d55", DefaultSlotMinutes = 15 },
                new Clinic { FacilityId = facility.Id, Name = "Cardiology", Code = "CARD", Specialty = ClinicSpecialty.Cardiology, ColorHex = "#0d8aaa", DefaultSlotMinutes = 30 }
            );
            db.Rooms.AddRange(
                new Room { FacilityId = facility.Id, Name = "Consulting Room 1", Code = "CR1" },
                new Room { FacilityId = facility.Id, Name = "Consulting Room 2", Code = "CR2" },
                new Room { FacilityId = facility.Id, Name = "Triage", Code = "TRG" }
            );
            await db.SaveChangesAsync();
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        // ----- Tenant System Administrator -----
        // Manages users / roles / facility settings inside one tenant. Has full operational
        // permissions but no platform-level access.
        var adminCfg = config.GetSection("Seed:Admin");
        var adminEmail = adminCfg["Email"] ?? "admin@thrivehealth.ng";
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FirstName = adminCfg["FirstName"] ?? "System",
                LastName = adminCfg["LastName"] ?? "Administrator",
                StaffNumber = "TH-ADM-0001",
                Designation = "System Administrator",
                Department = "IT",
                FacilityId = facility.Id,
                IsActive = true
            };
            var result = await userManager.CreateAsync(admin, adminCfg["Password"] ?? DemoPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, Roles.SystemAdministrator);
                await userManager.AddToRoleAsync(admin, Roles.MedicalDirector);
            }
        }

        // Cleanup: any pre-multi-tenant admin who was granted SuperAdmin gets it removed —
        // platform-level access now lives on the dedicated superadmin@ account below.
        if (admin is not null && await userManager.IsInRoleAsync(admin, Roles.SuperAdmin))
            await userManager.RemoveFromRoleAsync(admin, Roles.SuperAdmin);

        // ----- Platform Super Admin -----
        // Cross-tenant operator. Sees /superadmin/* only — no tenant data, no facility, no
        // user-management permissions inside any tenant. Operates from admin.thrivehealth.ng.
        if (!await roleManager.RoleExistsAsync(Roles.SuperAdmin))
            await roleManager.CreateAsync(new IdentityRole(Roles.SuperAdmin));

        var superCfg = config.GetSection("Seed:SuperAdmin");
        var superEmail = superCfg["Email"] ?? "superadmin@thrivehealth.ng";
        var superAdmin = await userManager.FindByEmailAsync(superEmail);
        if (superAdmin is null)
        {
            superAdmin = new ApplicationUser
            {
                UserName = superEmail,
                Email = superEmail,
                EmailConfirmed = true,
                FirstName = superCfg["FirstName"] ?? "Platform",
                LastName = superCfg["LastName"] ?? "Owner",
                StaffNumber = "TH-SU-0001",
                Designation = "Platform Owner",
                Department = "ThriveHealth",
                // Deliberately no FacilityId / TenantId — a SuperAdmin doesn't belong to any
                // single hospital, and the per-request tenant resolver should never pin them to one.
                FacilityId = null,
                TenantId = null,
                IsActive = true
            };
            var result = await userManager.CreateAsync(superAdmin, superCfg["Password"] ?? DemoPassword);
            if (result.Succeeded)
                await userManager.AddToRoleAsync(superAdmin, Roles.SuperAdmin);
        }
        else if (!await userManager.IsInRoleAsync(superAdmin, Roles.SuperAdmin))
        {
            await userManager.AddToRoleAsync(superAdmin, Roles.SuperAdmin);
        }

        var demoDoc = await userManager.FindByEmailAsync("doc@thrivehealth.ng");
        if (demoDoc is null)
        {
            demoDoc = new ApplicationUser
            {
                UserName = "doc@thrivehealth.ng",
                Email = "doc@thrivehealth.ng",
                EmailConfirmed = true,
                FirstName = "Chinedu",
                LastName = "Adeyemi",
                StaffNumber = "TH-DOC-0001",
                Designation = "Senior Registrar",
                Department = "Internal Medicine",
                LicenseBody = "MDCN",
                LicenseNumber = "MDCN/2018/45678",
                FacilityId = facility.Id,
                IsActive = true
            };
            var r = await userManager.CreateAsync(demoDoc, DemoPassword);
            if (r.Succeeded) await userManager.AddToRoleAsync(demoDoc, Roles.Doctor);
        }

        var demoNurse = await userManager.FindByEmailAsync("nurse@thrivehealth.ng");
        if (demoNurse is null)
        {
            demoNurse = new ApplicationUser
            {
                UserName = "nurse@thrivehealth.ng",
                Email = "nurse@thrivehealth.ng",
                EmailConfirmed = true,
                FirstName = "Aisha",
                LastName = "Bello",
                StaffNumber = "TH-NRS-0001",
                Designation = "Staff Nurse",
                Department = "OPD",
                LicenseBody = "NMCN",
                LicenseNumber = "NMCN/2020/12345",
                FacilityId = facility.Id,
                IsActive = true
            };
            var r = await userManager.CreateAsync(demoNurse, DemoPassword);
            if (r.Succeeded) await userManager.AddToRoleAsync(demoNurse, Roles.Nurse);
        }

        var opd = await db.Clinics.FirstAsync(c => c.FacilityId == facility.Id && c.Code == "OPD");
        if (!await db.ClinicianAvailabilities.AnyAsync(a => a.ClinicId == opd.Id))
        {
            for (int d = 1; d <= 5; d++)
            {
                db.ClinicianAvailabilities.Add(new ClinicianAvailability
                {
                    ClinicId = opd.Id,
                    ClinicianId = demoDoc.Id,
                    DayOfWeek = (DayOfWeek)d,
                    StartTime = new TimeOnly(8, 0),
                    EndTime = new TimeOnly(13, 0)
                });
            }
            await db.SaveChangesAsync();
        }

        if (!await db.IcdCodes.AnyAsync())
        {
            db.IcdCodes.AddRange(SeedIcdCodes());
            await db.SaveChangesAsync();
        }

        if (!await db.Drugs.AnyAsync())
        {
            db.Drugs.AddRange(SeedDrugs());
            await db.SaveChangesAsync();
        }

        if (!await db.DrugInteractions.AnyAsync())
        {
            db.DrugInteractions.AddRange(SeedInteractions());
            await db.SaveChangesAsync();
        }

        if (!await db.PharmacyStores.AnyAsync(s => s.FacilityId == facility.Id))
        {
            db.PharmacyStores.AddRange(
                new PharmacyStore { FacilityId = facility.Id, Name = "Main Pharmacy", Code = "MAIN", Type = StoreType.MainPharmacy },
                new PharmacyStore { FacilityId = facility.Id, Name = "Emergency", Code = "EMRG", Type = StoreType.Emergency }
            );
            await db.SaveChangesAsync();
        }

        var mainStore = await db.PharmacyStores.FirstAsync(s => s.FacilityId == facility.Id && s.Code == "MAIN");
        if (!await db.DrugStocks.AnyAsync(s => s.StoreId == mainStore.Id))
        {
            var drugs = await db.Drugs.ToListAsync();
            foreach (var d in drugs)
            {
                var batch = $"BATCH-{DateTime.UtcNow.Year}-{d.Id:D4}";
                var qty = d.IsControlled ? 50 : 200;
                var stock = new DrugStock
                {
                    DrugId = d.Id,
                    StoreId = mainStore.Id,
                    BatchNumber = batch,
                    ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)),
                    QuantityOnHand = qty,
                    UnitCost = d.UnitPrice
                };
                db.DrugStocks.Add(stock);
                db.StockMovements.Add(new StockMovement
                {
                    DrugId = d.Id, StoreId = mainStore.Id,
                    BatchNumber = batch,
                    ExpiryDate = stock.ExpiryDate,
                    Kind = StockMovementKind.OpeningBalance,
                    Quantity = qty, RunningBalance = qty,
                    UnitCost = d.UnitPrice,
                    Notes = "Demo opening balance"
                });
            }
            await db.SaveChangesAsync();
        }

        if (!await db.Clinics.AnyAsync(c => c.FacilityId == facility.Id && c.Code == "AE"))
        {
            db.Clinics.Add(new Clinic
            {
                FacilityId = facility.Id,
                Name = "Accident & Emergency",
                Code = "AE",
                Specialty = ClinicSpecialty.GeneralOpd,
                ColorHex = "#c81e1e",
                DefaultSlotMinutes = 15
            });
            await db.SaveChangesAsync();
        }

        if (!await db.ResusBays.AnyAsync(r => r.FacilityId == facility.Id))
        {
            db.ResusBays.AddRange(
                new ResusBay { FacilityId = facility.Id, Name = "Resus 1", Code = "R1", IsTraumaBay = false },
                new ResusBay { FacilityId = facility.Id, Name = "Resus 2", Code = "R2", IsTraumaBay = false },
                new ResusBay { FacilityId = facility.Id, Name = "Trauma Bay", Code = "T1", IsTraumaBay = true },
                new ResusBay { FacilityId = facility.Id, Name = "Paediatric Resus", Code = "PR", IsTraumaBay = false }
            );
            await db.SaveChangesAsync();
        }

        if (!await db.Wards.AnyAsync(w => w.FacilityId == facility.Id))
        {
            var male = new Ward { FacilityId = facility.Id, Name = "Male Medical Ward", Code = "MMW", Type = WardType.Male, ColorHex = "#1f6feb" };
            var female = new Ward { FacilityId = facility.Id, Name = "Female Medical Ward", Code = "FMW", Type = WardType.Female, ColorHex = "#c81e1e" };
            var paeds = new Ward { FacilityId = facility.Id, Name = "Paediatric Ward", Code = "PAED", Type = WardType.Paediatric, ColorHex = "#1f9d55" };
            var maternity = new Ward { FacilityId = facility.Id, Name = "Maternity Ward", Code = "MAT", Type = WardType.Maternity, ColorHex = "#b46a00" };
            var icu = new Ward { FacilityId = facility.Id, Name = "Intensive Care Unit", Code = "ICU", Type = WardType.Icu, ColorHex = "#0f1f3a" };

            db.Wards.AddRange(male, female, paeds, maternity, icu);
            await db.SaveChangesAsync();

            void AddBeds(Ward w, int count, BedRestriction r = BedRestriction.None)
            {
                for (int i = 1; i <= count; i++)
                    db.Beds.Add(new Bed { WardId = w.Id, BedNumber = $"{w.Code}-{i:D2}", Restriction = r });
            }
            AddBeds(male, 12, BedRestriction.MaleOnly);
            AddBeds(female, 12, BedRestriction.FemaleOnly);
            AddBeds(paeds, 8, BedRestriction.PaediatricOnly);
            AddBeds(maternity, 10, BedRestriction.FemaleOnly);
            AddBeds(icu, 6);
            await db.SaveChangesAsync();
        }

        if (!await db.LabTests.AnyAsync())
        {
            foreach (var t in SeedLabTests())
                db.LabTests.Add(t);
            await db.SaveChangesAsync();
        }

        if (!await db.Payers.AnyAsync())
        {
            db.Payers.AddRange(SeedPayers());
            await db.SaveChangesAsync();

            var allDrugs = await db.Drugs.ToListAsync();
            var plans = await db.PayerPlans.ToListAsync();
            foreach (var plan in plans)
            {
                if (plan.Code == "NHIA-FORMAL" || plan.Code == "NHIA-BHCPF")
                {
                    foreach (var d in allDrugs)
                    {
                        var covered = !d.IsControlled && d.Category != "Antimalarial (innovator brand)";
                        db.PayerFormularies.Add(new PayerFormulary { PayerPlanId = plan.Id, DrugId = d.Id, IsCovered = covered, CopayPercent = 0 });
                    }
                }
                else if (plan.Code == "HYG-PREMIUM" || plan.Code == "AXAM-EXEC")
                {
                    foreach (var d in allDrugs)
                        db.PayerFormularies.Add(new PayerFormulary { PayerPlanId = plan.Id, DrugId = d.Id, IsCovered = true, CopayPercent = 0 });
                }
                else if (plan.Code == "HYG-STD" || plan.Code == "REL-STD" || plan.Code == "AVON-STD" || plan.Code == "LEAD-STD")
                {
                    foreach (var d in allDrugs)
                    {
                        var brand = !string.IsNullOrEmpty(d.BrandName);
                        var covered = !d.IsControlled;
                        decimal copay = brand ? 30m : 10m;
                        db.PayerFormularies.Add(new PayerFormulary { PayerPlanId = plan.Id, DrugId = d.Id, IsCovered = covered, CopayPercent = covered ? copay : 0 });
                    }
                }
            }
            await db.SaveChangesAsync();
        }

        if (!await db.Suppliers.AnyAsync())
        {
            db.Suppliers.AddRange(SeedSuppliers());
            await db.SaveChangesAsync();
        }

        if (!await db.Theatres.AnyAsync(t => t.FacilityId == facility.Id))
        {
            db.Theatres.AddRange(
                new ThriveHealth.Web.Models.Theatre.Theatre { FacilityId = facility.Id, Name = "Theatre 1 (Main)", Code = "T1", Specialty = "General surgery", IsEmergencyTheatre = false },
                new ThriveHealth.Web.Models.Theatre.Theatre { FacilityId = facility.Id, Name = "Theatre 2 (Obstetric)", Code = "T2", Specialty = "Obstetric / Gynaecology", IsEmergencyTheatre = false },
                new ThriveHealth.Web.Models.Theatre.Theatre { FacilityId = facility.Id, Name = "Emergency Theatre", Code = "ET", Specialty = "Emergency surgery", IsEmergencyTheatre = true }
            );
            await db.SaveChangesAsync();
        }

        var demoCashier = await userManager.FindByEmailAsync("cashier@thrivehealth.ng");
        if (demoCashier is null)
        {
            demoCashier = new ApplicationUser
            {
                UserName = "cashier@thrivehealth.ng",
                Email = "cashier@thrivehealth.ng",
                EmailConfirmed = true,
                FirstName = "Folake",
                LastName = "Adeniran",
                StaffNumber = "TH-CSH-0001",
                Designation = "Senior Cashier",
                Department = "Finance",
                FacilityId = facility.Id,
                IsActive = true
            };
            var r = await userManager.CreateAsync(demoCashier, DemoPassword);
            if (r.Succeeded) await userManager.AddToRoleAsync(demoCashier, Roles.Cashier);
        }

        var staffWithoutHr = await db.Users
            .Where(u => u.FacilityId == facility.Id && !db.HrProfiles.Any(h => h.UserId == u.Id))
            .ToListAsync();
        foreach (var s in staffWithoutHr)
        {
            db.HrProfiles.Add(new HrProfile
            {
                UserId = s.Id,
                EmploymentType = EmploymentType.Permanent,
                Status = EmploymentStatus.Active,
                HireDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2)),
                Position = s.Designation,
                UnitOrSection = s.Department,
                GrossMonthlySalary = (s.Designation?.Contains("Senior") ?? false) ? 450_000m : 250_000m
            });
        }
        if (staffWithoutHr.Any()) await db.SaveChangesAsync();

        if (!await db.Vaccines.AnyAsync())
        {
            SeedVaccineCatalog(db);
            await db.SaveChangesAsync();
        }

        if (!await db.NotifiableDiseases.AnyAsync())
        {
            db.NotifiableDiseases.AddRange(SeedNotifiableDiseases());
            await db.SaveChangesAsync();
        }

        if (!await db.InventoryItems.AnyAsync())
        {
            db.InventoryItems.AddRange(SeedInventoryItems());
            await db.SaveChangesAsync();

            var central = await db.PharmacyStores.FirstOrDefaultAsync(s => s.FacilityId == facility.Id && s.Code == "MAIN");
            if (central != null)
            {
                var items = await db.InventoryItems.ToListAsync();
                foreach (var i in items)
                {
                    var batch = $"INV-{DateTime.UtcNow.Year}-{i.Id:D4}";
                    var qty = i.Category == InventoryCategory.Equipment ? 5 : 200;
                    db.InventoryStocks.Add(new InventoryStock
                    {
                        InventoryItemId = i.Id, StoreId = central.Id,
                        BatchNumber = batch,
                        ExpiryDate = i.IsExpiringTracked ? DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)) : null,
                        QuantityOnHand = qty, UnitCost = i.UnitPrice
                    });
                    db.InventoryStockMovements.Add(new InventoryStockMovement
                    {
                        InventoryItemId = i.Id, StoreId = central.Id,
                        BatchNumber = batch,
                        ExpiryDate = i.IsExpiringTracked ? DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)) : null,
                        Kind = InventoryMovementKind.OpeningBalance,
                        Quantity = qty, RunningBalance = qty,
                        UnitCost = i.UnitPrice,
                        Notes = "Demo opening balance"
                    });
                }
                await db.SaveChangesAsync();
            }
        }

        var demoLab = await userManager.FindByEmailAsync("lab@thrivehealth.ng");
        if (demoLab is null)
        {
            demoLab = new ApplicationUser
            {
                UserName = "lab@thrivehealth.ng",
                Email = "lab@thrivehealth.ng",
                EmailConfirmed = true,
                FirstName = "Tunde",
                LastName = "Akinwale",
                StaffNumber = "TH-LAB-0001",
                Designation = "Senior Lab Scientist",
                Department = "Laboratory",
                LicenseBody = "MLSCN",
                LicenseNumber = "MLSCN/2017/2233",
                FacilityId = facility.Id,
                IsActive = true
            };
            var r = await userManager.CreateAsync(demoLab, DemoPassword);
            if (r.Succeeded) await userManager.AddToRoleAsync(demoLab, Roles.LabScientist);
        }

        var demoRad = await userManager.FindByEmailAsync("rad@thrivehealth.ng");
        if (demoRad is null)
        {
            demoRad = new ApplicationUser
            {
                UserName = "rad@thrivehealth.ng",
                Email = "rad@thrivehealth.ng",
                EmailConfirmed = true,
                FirstName = "Halima",
                LastName = "Sani",
                StaffNumber = "TH-RAD-0001",
                Designation = "Senior Radiographer",
                Department = "Radiology",
                LicenseBody = "RRBN",
                LicenseNumber = "RRBN/2019/4455",
                FacilityId = facility.Id,
                IsActive = true
            };
            var r = await userManager.CreateAsync(demoRad, DemoPassword);
            if (r.Succeeded) await userManager.AddToRoleAsync(demoRad, Roles.Radiographer);
        }

        // ---- Demo users for the remaining roles (one per role) ----
        var demoRoleUsers = new[]
        {
            // (email, password, role, first, last, staff #, designation, department, licenseBody, licenseNumber)
            ("md@thrivehealth.ng",        DemoPassword,  Roles.MedicalDirector,        "Adebola", "Akande",   "TH-MD-0001",  "Medical Director",        "Executive",          "MDCN",   "MDCN/2002/00099"),
            ("ceo@thrivehealth.ng",       DemoPassword,       Roles.ChiefExecutive,         "Funmi",   "Bakare",   "TH-CEO-0001", "Chief Executive",         "Executive",          (string?)null, (string?)null),
            ("cfo@thrivehealth.ng",       DemoPassword,       Roles.ChiefFinancialOfficer,  "Olumide", "Eze",      "TH-CFO-0001", "Chief Financial Officer", "Finance",            null, null),
            ("consultant@thrivehealth.ng",DemoPassword,   Roles.Consultant,             "Kemi",    "Olawale",  "TH-CON-0001", "Consultant Physician",    "Internal Medicine",  "MDCN",   "MDCN/2008/12345"),
            ("mo@thrivehealth.ng",        DemoPassword,   Roles.MedicalOfficer,         "Tunde",   "Lawal",    "TH-MO-0001",  "Medical Officer",         "General Practice",   "MDCN",   "MDCN/2020/77123"),
            ("cno@thrivehealth.ng",       DemoPassword,   Roles.ChiefNursingOfficer,    "Grace",   "Iwu",      "TH-CNO-0001", "Chief Nursing Officer",   "Nursing",            "NMCN",   "NMCN/1998/00112"),
            ("midwife@thrivehealth.ng",   DemoPassword,   Roles.Midwife,                "Ngozi",   "Okeke",    "TH-MW-0001",  "Senior Midwife",          "Maternity",          "NMCN",   "NMCN/2014/22390"),
            ("pharm@thrivehealth.ng",     DemoPassword,     Roles.Pharmacist,             "Ifeoma",  "Nwankwo",  "TH-PHM-0001", "Senior Pharmacist",       "Pharmacy",           "PCN",    "PCN/2015/8810"),
            ("phtech@thrivehealth.ng",    DemoPassword,    Roles.PharmacyTechnician,     "Bashir",  "Yusuf",    "TH-PHT-0001", "Pharmacy Technician",     "Pharmacy",           "PCN",    "PCN-T/2021/3320"),
            ("labtech@thrivehealth.ng",   DemoPassword,   Roles.LabTechnician,          "Aisha",   "Garba",    "TH-LBT-0001", "Lab Technician",          "Laboratory",         "MLSCN",  "MLSCN-T/2022/5566"),
            ("physio@thrivehealth.ng",    DemoPassword,    Roles.Physiotherapist,        "Tobi",    "Adesanya", "TH-PHY-0001", "Senior Physiotherapist",  "Physiotherapy",      "MRTBN",  "MRTBN/2017/4421"),
            ("recep@thrivehealth.ng",     DemoPassword,     Roles.Receptionist,           "Blessing","Eze",      "TH-RCP-0001", "Front-desk Receptionist", "Front Office",       null, null),
            ("records@thrivehealth.ng",   DemoPassword,   Roles.RecordsOfficer,         "Yetunde", "Olu",      "TH-REC-0001", "Records Officer",         "Health Records",     null, null),
            ("triage@thrivehealth.ng",    DemoPassword,    Roles.TriageClerk,            "Sade",    "Bello",    "TH-TRG-0001", "Triage Clerk",            "Front Office",       null, null),
            ("acct@thrivehealth.ng",      DemoPassword,      Roles.Accountant,             "Emeka",   "Onuoha",   "TH-ACT-0001", "Accountant",              "Finance",            null, null),
            ("claims@thrivehealth.ng",    DemoPassword,    Roles.ClaimsOfficer,          "Hadiza",  "Mohammed", "TH-CLM-0001", "Claims Officer",          "Finance",            null, null),
            ("hr@thrivehealth.ng",        DemoPassword,        Roles.HrOfficer,              "Patience","Ojo",      "TH-HR-0001",  "HR Officer",              "Human Resources",    null, null),
            ("procure@thrivehealth.ng",   DemoPassword,   Roles.ProcurementOfficer,     "Suleiman","Bala",     "TH-PRC-0001", "Procurement Officer",     "Procurement",        null, null),
            ("store@thrivehealth.ng",     DemoPassword,     Roles.StoreOfficer,           "Ada",     "Nnamdi",   "TH-STR-0001", "Central Store Officer",   "Stores",             null, null),
            ("biomed@thrivehealth.ng",    DemoPassword,    Roles.BiomedicalEngineer,     "Ibrahim", "Suleiman", "TH-BME-0001", "Biomedical Engineer",     "Biomedical",         null, null),
            ("nurse2@thrivehealth.ng",    DemoPassword,     Roles.Nurse,                  "Maryam",  "Yakubu",   "TH-NRS-0002", "Staff Nurse",             "A&E",                "NMCN",   "NMCN/2019/55432"),
            ("ph@thrivehealth.ng",        DemoPassword, Roles.PublicHealthOfficer,    "Ngozi",   "Eze",      "TH-PHO-0001", "Public Health Officer",   "Public Health",      null, null)
        };

        foreach (var u in demoRoleUsers)
        {
            if (await userManager.FindByEmailAsync(u.Item1) is not null) continue;
            var user = new ApplicationUser
            {
                UserName = u.Item1,
                Email = u.Item1,
                EmailConfirmed = true,
                FirstName = u.Item4,
                LastName = u.Item5,
                StaffNumber = u.Item6,
                Designation = u.Item7,
                Department = u.Item8,
                LicenseBody = u.Item9,
                LicenseNumber = u.Item10,
                FacilityId = facility.Id,
                IsActive = true
            };
            var res = await userManager.CreateAsync(user, u.Item2);
            if (res.Succeeded) await userManager.AddToRoleAsync(user, u.Item3);
        }

        // Demo-mode password sync: idempotently force every demo user's password to the
        // unified DemoPassword. This catches accounts that were created on earlier seed
        // runs with per-role passwords (Doctor@12345, Nurse@12345, …) and brings them
        // in line on the next startup without needing a DB wipe.
        var demoEmails = demoRoleUsers.Select(x => x.Item1)
            .Append("admin@thrivehealth.ng")
            .Append("superadmin@thrivehealth.ng")
            .Append("doc@thrivehealth.ng")
            .Append("nurse@thrivehealth.ng")
            .Append("cashier@thrivehealth.ng")
            .Append("lab@thrivehealth.ng")
            .Append("rad@thrivehealth.ng")
            .ToList();

        foreach (var email in demoEmails)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null) continue;
            if (await userManager.CheckPasswordAsync(user, DemoPassword)) continue; // already in sync
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            await userManager.ResetPasswordAsync(user, token, DemoPassword);
        }
    }

    private static IEnumerable<LabTest> SeedLabTests()
    {
        LabTest Test(string code, string name, LabSection sec, string? specimen, string? container, int tat, decimal? price, params LabAnalyte[] analytes)
        {
            var t = new LabTest { Code = code, Name = name, Section = sec, Specimen = specimen, Container = container, TurnaroundHours = tat, Price = price };
            for (int i = 0; i < analytes.Length; i++)
            {
                analytes[i].SortOrder = i;
                t.Analytes.Add(analytes[i]);
            }
            return t;
        }
        LabAnalyte A(string name, string? unit = null, decimal? lo = null, decimal? hi = null, decimal? cLo = null, decimal? cHi = null)
            => new LabAnalyte { Name = name, Unit = unit, RefLow = lo, RefHigh = hi, CriticalLow = cLo, CriticalHigh = cHi };

        return new[]
        {
            // ---- Haematology ----
            Test("FBC", "Full Blood Count", LabSection.Haematology, "Whole blood", "EDTA (purple)", 2, 2500m,
                A("WBC", "x10⁹/L", 4.0m, 11.0m, 1.0m, 30.0m),
                A("RBC", "x10¹²/L", 4.5m, 5.5m),
                A("Haemoglobin", "g/dL", 12.0m, 16.0m, 6.0m, 20.0m),
                A("Haematocrit", "%", 36m, 48m, 18m, 60m),
                A("MCV", "fL", 80m, 100m),
                A("MCH", "pg", 27m, 32m),
                A("MCHC", "g/dL", 32m, 36m),
                A("Platelets", "x10⁹/L", 150m, 400m, 20m, 1000m),
                A("Neutrophils %", "%", 40m, 75m),
                A("Lymphocytes %", "%", 20m, 45m),
                A("Eosinophils %", "%", 1m, 6m),
                A("Monocytes %", "%", 2m, 10m)),

            Test("ESR", "Erythrocyte Sedimentation Rate", LabSection.Haematology, "Whole blood", "EDTA (purple)", 2, 600m,
                A("ESR", "mm/hr", 0m, 20m)),

            Test("PERIPHERAL", "Peripheral blood film", LabSection.Haematology, "Whole blood", "EDTA (purple)", 4, 1500m,
                A("RBC morphology"), A("WBC morphology"), A("Platelet morphology"), A("Parasites seen")),

            Test("RETIC", "Reticulocyte count", LabSection.Haematology, "Whole blood", "EDTA (purple)", 4, 1200m,
                A("Reticulocyte %", "%", 0.5m, 2.5m)),

            Test("PT_INR", "Prothrombin Time / INR", LabSection.Coagulation, "Citrated plasma", "Sodium citrate (light blue)", 4, 2000m,
                A("PT", "sec", 11m, 14m),
                A("INR", null, 0.9m, 1.2m, null, 5m)),

            Test("APTT", "Activated Partial Thromboplastin Time", LabSection.Coagulation, "Citrated plasma", "Sodium citrate (light blue)", 4, 1500m,
                A("APTT", "sec", 25m, 35m)),

            Test("GROUP_RHESUS", "Blood Group + Rhesus", LabSection.BloodBank, "Whole blood", "EDTA (purple)", 1, 1500m,
                A("ABO group"), A("Rhesus")),

            // ---- Chemistry ----
            Test("UE", "Urea & Electrolytes", LabSection.Chemistry, "Serum", "SST (gold)", 4, 3500m,
                A("Sodium", "mmol/L", 135m, 145m, 120m, 160m),
                A("Potassium", "mmol/L", 3.5m, 5.0m, 2.5m, 6.5m),
                A("Chloride", "mmol/L", 98m, 107m),
                A("Bicarbonate", "mmol/L", 22m, 29m, 10m, null),
                A("Urea", "mmol/L", 2.5m, 7.8m, null, 30m),
                A("Creatinine", "µmol/L", 60m, 110m, null, 600m)),

            Test("LFT", "Liver Function Tests", LabSection.Chemistry, "Serum", "SST (gold)", 4, 4000m,
                A("Total bilirubin", "µmol/L", 5m, 21m),
                A("Direct bilirubin", "µmol/L", 0m, 5m),
                A("AST", "U/L", 5m, 40m),
                A("ALT", "U/L", 5m, 40m),
                A("ALP", "U/L", 40m, 130m),
                A("Total protein", "g/L", 60m, 80m),
                A("Albumin", "g/L", 35m, 50m, 15m, null)),

            Test("LIPID", "Lipid Profile", LabSection.Chemistry, "Serum (fasting preferred)", "SST (gold)", 6, 3500m,
                A("Total cholesterol", "mmol/L", null, 5.2m),
                A("LDL", "mmol/L", null, 3.4m),
                A("HDL", "mmol/L", 1.0m, null),
                A("Triglycerides", "mmol/L", null, 1.7m)),

            Test("GLUCOSE", "Random Blood Glucose", LabSection.Chemistry, "Plasma / capillary", "Fluoride (grey)", 1, 800m,
                A("Glucose", "mmol/L", 4.0m, 7.8m, 2.0m, 25m)),

            Test("HBA1C", "Glycated Haemoglobin", LabSection.Chemistry, "Whole blood", "EDTA (purple)", 6, 5000m,
                A("HbA1c", "%", 4.0m, 6.0m)),

            Test("CRP", "C-Reactive Protein", LabSection.Chemistry, "Serum", "SST (gold)", 2, 2500m,
                A("CRP", "mg/L", 0m, 10m)),

            Test("PROCALC", "Procalcitonin", LabSection.Chemistry, "Serum", "SST (gold)", 4, 8500m,
                A("Procalcitonin", "ng/mL", 0m, 0.5m, null, 10m)),

            Test("LACTATE", "Lactate", LabSection.Chemistry, "Whole blood", "Heparin (green)", 1, 2500m,
                A("Lactate", "mmol/L", 0.5m, 2.2m, null, 4.0m)),

            Test("TROPONIN", "Troponin I (high-sensitivity)", LabSection.Chemistry, "Serum", "SST (gold)", 1, 6500m,
                A("Troponin I", "ng/L", null, 14m, null, 100m)),

            Test("AMYLASE", "Amylase", LabSection.Chemistry, "Serum", "SST (gold)", 4, 2500m,
                A("Amylase", "U/L", 30m, 110m, null, 1000m)),

            Test("CALCIUM", "Calcium + Phosphate", LabSection.Chemistry, "Serum", "SST (gold)", 4, 2500m,
                A("Calcium", "mmol/L", 2.15m, 2.55m, 1.7m, 3.5m),
                A("Phosphate", "mmol/L", 0.8m, 1.5m)),

            // ---- Endocrinology ----
            Test("TFT", "Thyroid Function Tests", LabSection.Endocrinology, "Serum", "SST (gold)", 24, 6500m,
                A("TSH", "mIU/L", 0.3m, 4.5m),
                A("Free T4", "pmol/L", 12m, 22m),
                A("Free T3", "pmol/L", 3.5m, 7.0m)),

            Test("HCG", "β-hCG (pregnancy)", LabSection.Endocrinology, "Serum", "SST (gold)", 4, 3500m,
                A("Beta-hCG", "IU/L", null, 5m)),

            // ---- Microbiology / Parasitology ----
            Test("MP_RDT", "Malaria Parasite (RDT + microscopy)", LabSection.Parasitology, "Whole blood", "EDTA (purple)", 1, 1500m,
                A("RDT result"), A("Species"), A("Density (per µL)")),

            Test("WIDAL", "Widal test", LabSection.Microbiology, "Serum", "SST (gold)", 4, 1500m,
                A("S. typhi O", null, null, 80m), A("S. typhi H", null, null, 160m),
                A("S. paratyphi A"), A("S. paratyphi B"), A("S. paratyphi C")),

            Test("HIV_SCR", "HIV screening (4th-gen Ag/Ab)", LabSection.Immunology, "Serum / plasma", "SST (gold)", 4, 2500m,
                A("HIV Ag/Ab")),

            Test("HBSAG", "Hepatitis B surface antigen", LabSection.Immunology, "Serum", "SST (gold)", 4, 2500m,
                A("HBsAg")),

            Test("HCV_AB", "Hepatitis C antibody", LabSection.Immunology, "Serum", "SST (gold)", 4, 2500m,
                A("Anti-HCV")),

            Test("URINALYSIS", "Urinalysis (dipstick + microscopy)", LabSection.Microbiology, "Urine (mid-stream)", "Sterile urine cup", 1, 1500m,
                A("Specific gravity"), A("pH"),
                A("Protein"), A("Glucose"), A("Ketones"),
                A("Blood"), A("Leukocytes"), A("Nitrites"),
                A("Bilirubin"), A("Urobilinogen")),

            Test("URINE_MCS", "Urine microscopy, culture & sensitivity", LabSection.Microbiology, "Urine (mid-stream)", "Sterile urine cup", 72, 5500m,
                A("Microscopy"), A("Culture"), A("Sensitivity")),

            Test("BLOOD_CULTURE", "Blood culture (aerobic + anaerobic)", LabSection.Microbiology, "Whole blood", "Blood culture bottles", 72, 8500m,
                A("Aerobic"), A("Anaerobic"), A("Organism"), A("Sensitivity"))
        };
    }

    private static IEnumerable<IcdCode> SeedIcdCodes() => new[]
    {
        new IcdCode { Code = "B54",   Description = "Malaria, unspecified", Category = "Infectious", LocalSynonyms = "malaria, fever and shivering" },
        new IcdCode { Code = "B50",   Description = "Plasmodium falciparum malaria", Category = "Infectious", LocalSynonyms = "severe malaria" },
        new IcdCode { Code = "A01.0", Description = "Typhoid fever", Category = "Infectious", LocalSynonyms = "typhoid, enteric fever" },
        new IcdCode { Code = "A09",   Description = "Diarrhoea and gastroenteritis of presumed infectious origin", Category = "Infectious", LocalSynonyms = "belle dey run, runny stomach, gastroenteritis" },
        new IcdCode { Code = "J18.9", Description = "Pneumonia, unspecified", Category = "Respiratory", LocalSynonyms = "pneumonia, chest infection" },
        new IcdCode { Code = "J45",   Description = "Asthma", Category = "Respiratory", LocalSynonyms = "asthma, wheezing" },
        new IcdCode { Code = "J06.9", Description = "Acute upper respiratory infection, unspecified", Category = "Respiratory", LocalSynonyms = "URTI, common cold, catarrh" },
        new IcdCode { Code = "I10",   Description = "Essential (primary) hypertension", Category = "Cardiovascular", LocalSynonyms = "hypertension, high BP, HTN" },
        new IcdCode { Code = "I50.9", Description = "Heart failure, unspecified", Category = "Cardiovascular", LocalSynonyms = "heart failure, CHF" },
        new IcdCode { Code = "I63",   Description = "Cerebral infarction", Category = "Cardiovascular", LocalSynonyms = "stroke, CVA" },
        new IcdCode { Code = "E11",   Description = "Type 2 diabetes mellitus", Category = "Endocrine", LocalSynonyms = "diabetes, sugar disease, T2DM" },
        new IcdCode { Code = "E10",   Description = "Type 1 diabetes mellitus", Category = "Endocrine", LocalSynonyms = "T1DM, insulin-dependent diabetes" },
        new IcdCode { Code = "N39.0", Description = "Urinary tract infection, site not specified", Category = "Genitourinary", LocalSynonyms = "UTI, urine infection" },
        new IcdCode { Code = "N18",   Description = "Chronic kidney disease", Category = "Genitourinary", LocalSynonyms = "CKD, kidney disease" },
        new IcdCode { Code = "D57",   Description = "Sickle-cell disorders", Category = "Haematology", LocalSynonyms = "sickle cell, SCD, crisis" },
        new IcdCode { Code = "D50.9", Description = "Iron deficiency anaemia, unspecified", Category = "Haematology", LocalSynonyms = "anaemia, low blood" },
        new IcdCode { Code = "B20",   Description = "HIV disease", Category = "Infectious", LocalSynonyms = "HIV, AIDS, retroviral" },
        new IcdCode { Code = "A15",   Description = "Respiratory tuberculosis", Category = "Infectious", LocalSynonyms = "TB, tuberculosis" },
        new IcdCode { Code = "O14",   Description = "Pre-eclampsia", Category = "Maternity", LocalSynonyms = "preeclampsia, PIH" },
        new IcdCode { Code = "O15",   Description = "Eclampsia", Category = "Maternity", LocalSynonyms = "eclampsia, fits in pregnancy" },
        new IcdCode { Code = "O80",   Description = "Single spontaneous delivery", Category = "Maternity", LocalSynonyms = "SVD, normal delivery" },
        new IcdCode { Code = "P36",   Description = "Bacterial sepsis of newborn", Category = "Neonatal", LocalSynonyms = "neonatal sepsis" },
        new IcdCode { Code = "R50.9", Description = "Fever, unspecified", Category = "Symptoms", LocalSynonyms = "body hot, pyrexia, fever" },
        new IcdCode { Code = "R51",   Description = "Headache", Category = "Symptoms", LocalSynonyms = "head pain, headache" },
        new IcdCode { Code = "R10.4", Description = "Other and unspecified abdominal pain", Category = "Symptoms", LocalSynonyms = "belle pain, stomach ache" },
        new IcdCode { Code = "R11",   Description = "Nausea and vomiting", Category = "Symptoms", LocalSynonyms = "vomiting, throwing up" },
        new IcdCode { Code = "R05",   Description = "Cough", Category = "Symptoms", LocalSynonyms = "cough" },
        new IcdCode { Code = "M54.5", Description = "Low back pain", Category = "Musculoskeletal", LocalSynonyms = "back pain, LBP, waist pain" },
        new IcdCode { Code = "L08.9", Description = "Local infection of skin, unspecified", Category = "Skin", LocalSynonyms = "skin infection, boil" },
        new IcdCode { Code = "F32",   Description = "Depressive episode", Category = "Mental health", LocalSynonyms = "depression" }
    };

    private static IEnumerable<Drug> SeedDrugs() => new[]
    {
        // --- Antimalarials ---
        new Drug { GenericName = "Artemether-Lumefantrine", Strength = "80/480 mg", DoseForm = DoseForm.Tablet, Category = "Antimalarial", AtcCode = "P01BF01", NafdacNumber = "04-0123", Manufacturer = "May & Baker Nigeria", UnitOfIssue = "tab", UnitPrice = 250m, ReorderLevel = 50, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Artesunate", Strength = "60 mg", DoseForm = DoseForm.Injection, Category = "Antimalarial", AtcCode = "P01BE03", UnitOfIssue = "vial", UnitPrice = 1200m, ReorderLevel = 30, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Quinine sulphate", Strength = "300 mg", DoseForm = DoseForm.Tablet, Category = "Antimalarial", AtcCode = "P01BC01", UnitOfIssue = "tab", UnitPrice = 80m, ReorderLevel = 100, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Sulfadoxine-Pyrimethamine", Strength = "500/25 mg", DoseForm = DoseForm.Tablet, Category = "Antimalarial", UnitOfIssue = "tab", UnitPrice = 60m, ReorderLevel = 100, Schedule = DrugCategory.PrescriptionOnly },

        // --- Antibiotics ---
        new Drug { GenericName = "Amoxicillin", BrandName = "Ampiclox", Strength = "500 mg", DoseForm = DoseForm.Capsule, Category = "Antibiotic (penicillin)", AtcCode = "J01CA04", UnitOfIssue = "cap", UnitPrice = 50m, ReorderLevel = 200, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Amoxicillin-Clavulanate", BrandName = "Augmentin", Strength = "625 mg", DoseForm = DoseForm.Tablet, Category = "Antibiotic (penicillin)", AtcCode = "J01CR02", UnitOfIssue = "tab", UnitPrice = 350m, ReorderLevel = 100, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Ampicillin", Strength = "500 mg", DoseForm = DoseForm.Injection, Category = "Antibiotic (penicillin)", UnitOfIssue = "vial", UnitPrice = 200m, ReorderLevel = 50, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Penicillin V", Strength = "250 mg", DoseForm = DoseForm.Tablet, Category = "Antibiotic (penicillin)", UnitOfIssue = "tab", UnitPrice = 30m, ReorderLevel = 100, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Ceftriaxone", BrandName = "Rocephin", Strength = "1 g", DoseForm = DoseForm.Injection, Category = "Antibiotic (cephalosporin)", AtcCode = "J01DD04", UnitOfIssue = "vial", UnitPrice = 700m, ReorderLevel = 50, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Cefuroxime", Strength = "500 mg", DoseForm = DoseForm.Tablet, Category = "Antibiotic (cephalosporin)", UnitOfIssue = "tab", UnitPrice = 300m, ReorderLevel = 100, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Ciprofloxacin", BrandName = "Ciprotab", Strength = "500 mg", DoseForm = DoseForm.Tablet, Category = "Antibiotic (quinolone)", AtcCode = "J01MA02", UnitOfIssue = "tab", UnitPrice = 120m, ReorderLevel = 100, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Metronidazole", BrandName = "Flagyl", Strength = "400 mg", DoseForm = DoseForm.Tablet, Category = "Antibiotic", AtcCode = "P01AB01", UnitOfIssue = "tab", UnitPrice = 30m, ReorderLevel = 200, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Erythromycin", Strength = "250 mg", DoseForm = DoseForm.Tablet, Category = "Antibiotic (macrolide)", UnitOfIssue = "tab", UnitPrice = 70m, ReorderLevel = 100, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Azithromycin", BrandName = "Zithromax", Strength = "500 mg", DoseForm = DoseForm.Tablet, Category = "Antibiotic (macrolide)", AtcCode = "J01FA10", UnitOfIssue = "tab", UnitPrice = 250m, ReorderLevel = 100, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Doxycycline", Strength = "100 mg", DoseForm = DoseForm.Capsule, Category = "Antibiotic (tetracycline)", UnitOfIssue = "cap", UnitPrice = 80m, ReorderLevel = 100, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Cotrimoxazole", BrandName = "Septrin", Strength = "480 mg", DoseForm = DoseForm.Tablet, Category = "Antibiotic", UnitOfIssue = "tab", UnitPrice = 25m, ReorderLevel = 200, Schedule = DrugCategory.PrescriptionOnly },

        // --- Antihypertensives ---
        new Drug { GenericName = "Amlodipine", Strength = "10 mg", DoseForm = DoseForm.Tablet, Category = "Antihypertensive (CCB)", AtcCode = "C08CA01", UnitOfIssue = "tab", UnitPrice = 60m, ReorderLevel = 100, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Lisinopril", Strength = "10 mg", DoseForm = DoseForm.Tablet, Category = "Antihypertensive (ACEi)", AtcCode = "C09AA03", UnitOfIssue = "tab", UnitPrice = 80m, ReorderLevel = 100, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Losartan", Strength = "50 mg", DoseForm = DoseForm.Tablet, Category = "Antihypertensive (ARB)", AtcCode = "C09CA01", UnitOfIssue = "tab", UnitPrice = 100m, ReorderLevel = 100, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Hydrochlorothiazide", Strength = "25 mg", DoseForm = DoseForm.Tablet, Category = "Diuretic", AtcCode = "C03AA03", UnitOfIssue = "tab", UnitPrice = 40m, ReorderLevel = 100, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Furosemide", BrandName = "Lasix", Strength = "40 mg", DoseForm = DoseForm.Tablet, Category = "Diuretic", AtcCode = "C03CA01", UnitOfIssue = "tab", UnitPrice = 50m, ReorderLevel = 100, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Spironolactone", Strength = "25 mg", DoseForm = DoseForm.Tablet, Category = "Diuretic (K-sparing)", UnitOfIssue = "tab", UnitPrice = 80m, ReorderLevel = 100, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Atenolol", Strength = "50 mg", DoseForm = DoseForm.Tablet, Category = "Antihypertensive (β-blocker)", UnitOfIssue = "tab", UnitPrice = 50m, ReorderLevel = 100, Schedule = DrugCategory.PrescriptionOnly },

        // --- Antidiabetics ---
        new Drug { GenericName = "Metformin", BrandName = "Glucophage", Strength = "500 mg", DoseForm = DoseForm.Tablet, Category = "Antidiabetic (biguanide)", AtcCode = "A10BA02", UnitOfIssue = "tab", UnitPrice = 30m, ReorderLevel = 200, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Glibenclamide", Strength = "5 mg", DoseForm = DoseForm.Tablet, Category = "Antidiabetic (sulfonylurea)", UnitOfIssue = "tab", UnitPrice = 25m, ReorderLevel = 100, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Insulin (regular)", BrandName = "Actrapid", Strength = "100 IU/mL", DoseForm = DoseForm.Injection, Category = "Antidiabetic (insulin)", UnitOfIssue = "vial", UnitPrice = 4500m, ReorderLevel = 20, Schedule = DrugCategory.PrescriptionOnly },

        // --- Analgesics / NSAIDs ---
        new Drug { GenericName = "Paracetamol", BrandName = "Panadol", Strength = "500 mg", DoseForm = DoseForm.Tablet, Category = "Analgesic", AtcCode = "N02BE01", NafdacNumber = "04-9999", UnitOfIssue = "tab", UnitPrice = 10m, ReorderLevel = 500, Schedule = DrugCategory.OverTheCounter },
        new Drug { GenericName = "Ibuprofen", Strength = "400 mg", DoseForm = DoseForm.Tablet, Category = "NSAID", AtcCode = "M01AE01", UnitOfIssue = "tab", UnitPrice = 20m, ReorderLevel = 200, Schedule = DrugCategory.OverTheCounter },
        new Drug { GenericName = "Diclofenac", Strength = "50 mg", DoseForm = DoseForm.Tablet, Category = "NSAID", UnitOfIssue = "tab", UnitPrice = 25m, ReorderLevel = 200, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Aspirin", Strength = "75 mg", DoseForm = DoseForm.Tablet, Category = "Antiplatelet", AtcCode = "B01AC06", UnitOfIssue = "tab", UnitPrice = 15m, ReorderLevel = 200, Schedule = DrugCategory.OverTheCounter },
        new Drug { GenericName = "Tramadol", Strength = "50 mg", DoseForm = DoseForm.Capsule, Category = "Opioid (weak)", AtcCode = "N02AX02", UnitOfIssue = "cap", UnitPrice = 80m, ReorderLevel = 50, Schedule = DrugCategory.ControlledSchedule3 },
        new Drug { GenericName = "Morphine sulphate", Strength = "10 mg/mL", DoseForm = DoseForm.Injection, Category = "Opioid (strong)", AtcCode = "N02AA01", UnitOfIssue = "amp", UnitPrice = 600m, ReorderLevel = 20, Schedule = DrugCategory.ControlledSchedule1 },
        new Drug { GenericName = "Pethidine", Strength = "50 mg/mL", DoseForm = DoseForm.Injection, Category = "Opioid", UnitOfIssue = "amp", UnitPrice = 500m, ReorderLevel = 20, Schedule = DrugCategory.ControlledSchedule1 },

        // --- GI ---
        new Drug { GenericName = "Omeprazole", Strength = "20 mg", DoseForm = DoseForm.Capsule, Category = "PPI", AtcCode = "A02BC01", UnitOfIssue = "cap", UnitPrice = 70m, ReorderLevel = 100, Schedule = DrugCategory.OverTheCounter },
        new Drug { GenericName = "Hyoscine butylbromide", BrandName = "Buscopan", Strength = "10 mg", DoseForm = DoseForm.Tablet, Category = "Antispasmodic", UnitOfIssue = "tab", UnitPrice = 60m, ReorderLevel = 100, Schedule = DrugCategory.OverTheCounter },
        new Drug { GenericName = "ORS sachet", Strength = "20.5 g", DoseForm = DoseForm.Powder, Category = "Rehydration", UnitOfIssue = "sachet", UnitPrice = 100m, ReorderLevel = 100, Schedule = DrugCategory.OverTheCounter },
        new Drug { GenericName = "Loperamide", Strength = "2 mg", DoseForm = DoseForm.Capsule, Category = "Antidiarrhoeal", UnitOfIssue = "cap", UnitPrice = 40m, ReorderLevel = 100, Schedule = DrugCategory.OverTheCounter },

        // --- Respiratory ---
        new Drug { GenericName = "Salbutamol", BrandName = "Ventolin", Strength = "100 µg", DoseForm = DoseForm.Inhaler, Category = "Bronchodilator (β2-agonist)", AtcCode = "R03AC02", UnitOfIssue = "inhaler", UnitPrice = 1500m, ReorderLevel = 30, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Beclomethasone", Strength = "200 µg", DoseForm = DoseForm.Inhaler, Category = "Inhaled corticosteroid", UnitOfIssue = "inhaler", UnitPrice = 2200m, ReorderLevel = 20, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Prednisolone", Strength = "5 mg", DoseForm = DoseForm.Tablet, Category = "Corticosteroid", AtcCode = "H02AB06", UnitOfIssue = "tab", UnitPrice = 15m, ReorderLevel = 200, Schedule = DrugCategory.PrescriptionOnly },

        // --- HIV / TB ---
        new Drug { GenericName = "Tenofovir-Lamivudine-Dolutegravir", BrandName = "TLD", Strength = "300/300/50 mg", DoseForm = DoseForm.Tablet, Category = "ART (HIV)", UnitOfIssue = "tab", UnitPrice = 0m, ReorderLevel = 100, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Rifampicin-Isoniazid-Pyrazinamide-Ethambutol", BrandName = "RHZE", Strength = "150/75/400/275 mg", DoseForm = DoseForm.Tablet, Category = "Anti-TB", UnitOfIssue = "tab", UnitPrice = 0m, ReorderLevel = 200, Schedule = DrugCategory.PrescriptionOnly },

        // --- Maternal / OB ---
        new Drug { GenericName = "Folic acid", Strength = "5 mg", DoseForm = DoseForm.Tablet, Category = "Vitamin", AtcCode = "B03BB01", UnitOfIssue = "tab", UnitPrice = 5m, ReorderLevel = 500, Schedule = DrugCategory.OverTheCounter },
        new Drug { GenericName = "Ferrous sulphate", Strength = "200 mg", DoseForm = DoseForm.Tablet, Category = "Iron supplement", UnitOfIssue = "tab", UnitPrice = 8m, ReorderLevel = 500, Schedule = DrugCategory.OverTheCounter },
        new Drug { GenericName = "Oxytocin", Strength = "10 IU/mL", DoseForm = DoseForm.Injection, Category = "Uterotonic", UnitOfIssue = "amp", UnitPrice = 250m, ReorderLevel = 50, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Magnesium sulphate", Strength = "50% 10 mL", DoseForm = DoseForm.Injection, Category = "Anticonvulsant", UnitOfIssue = "amp", UnitPrice = 200m, ReorderLevel = 30, Schedule = DrugCategory.PrescriptionOnly },

        // --- CV ---
        new Drug { GenericName = "Warfarin", Strength = "5 mg", DoseForm = DoseForm.Tablet, Category = "Anticoagulant", AtcCode = "B01AA03", UnitOfIssue = "tab", UnitPrice = 60m, ReorderLevel = 50, Schedule = DrugCategory.PrescriptionOnly },
        new Drug { GenericName = "Atorvastatin", BrandName = "Lipitor", Strength = "20 mg", DoseForm = DoseForm.Tablet, Category = "Statin", AtcCode = "C10AA05", UnitOfIssue = "tab", UnitPrice = 100m, ReorderLevel = 100, Schedule = DrugCategory.PrescriptionOnly },

        // --- Sickle cell ---
        new Drug { GenericName = "Hydroxyurea", Strength = "500 mg", DoseForm = DoseForm.Capsule, Category = "Sickle cell modifier", UnitOfIssue = "cap", UnitPrice = 200m, ReorderLevel = 50, Schedule = DrugCategory.PrescriptionOnly },

        // --- Mental health ---
        new Drug { GenericName = "Diazepam", Strength = "5 mg", DoseForm = DoseForm.Tablet, Category = "Benzodiazepine", AtcCode = "N05BA01", UnitOfIssue = "tab", UnitPrice = 40m, ReorderLevel = 50, Schedule = DrugCategory.ControlledSchedule4 },
        new Drug { GenericName = "Fluoxetine", Strength = "20 mg", DoseForm = DoseForm.Capsule, Category = "SSRI", UnitOfIssue = "cap", UnitPrice = 80m, ReorderLevel = 100, Schedule = DrugCategory.PrescriptionOnly }
    };

    private static IEnumerable<DrugInteraction> SeedInteractions() => new[]
    {
        new DrugInteraction { DrugAKey = "warfarin", DrugBKey = "aspirin", Severity = InteractionSeverity.Severe, Note = "Increased bleeding risk. Avoid combination unless benefits clearly outweigh risk." },
        new DrugInteraction { DrugAKey = "warfarin", DrugBKey = "diclofenac", Severity = InteractionSeverity.Severe, Note = "Increased bleeding risk; NSAIDs displace warfarin from albumin." },
        new DrugInteraction { DrugAKey = "warfarin", DrugBKey = "ibuprofen", Severity = InteractionSeverity.Severe, Note = "Increased bleeding risk; NSAIDs interfere with warfarin." },
        new DrugInteraction { DrugAKey = "warfarin", DrugBKey = "metronidazole", Severity = InteractionSeverity.Severe, Note = "Metronidazole inhibits warfarin metabolism — INR will rise." },
        new DrugInteraction { DrugAKey = "warfarin", DrugBKey = "ciprofloxacin", Severity = InteractionSeverity.Moderate, Note = "Quinolones can potentiate warfarin effect — monitor INR closely." },
        new DrugInteraction { DrugAKey = "lisinopril", DrugBKey = "spironolactone", Severity = InteractionSeverity.Severe, Note = "Risk of hyperkalaemia from combined potassium retention." },
        new DrugInteraction { DrugAKey = "lisinopril", DrugBKey = "ibuprofen", Severity = InteractionSeverity.Moderate, Note = "NSAIDs reduce ACEi efficacy and increase AKI risk." },
        new DrugInteraction { DrugAKey = "losartan", DrugBKey = "spironolactone", Severity = InteractionSeverity.Severe, Note = "Risk of hyperkalaemia from combined potassium retention." },
        new DrugInteraction { DrugAKey = "atenolol", DrugBKey = "amlodipine", Severity = InteractionSeverity.Moderate, Note = "Additive hypotension — useful but monitor BP." },
        new DrugInteraction { DrugAKey = "ciprofloxacin", DrugBKey = "ferrous", Severity = InteractionSeverity.Moderate, Note = "Iron reduces ciprofloxacin absorption — separate by 2 hours." },
        new DrugInteraction { DrugAKey = "doxycycline", DrugBKey = "ferrous", Severity = InteractionSeverity.Moderate, Note = "Iron reduces doxycycline absorption — separate by 2 hours." },
        new DrugInteraction { DrugAKey = "metformin", DrugBKey = "furosemide", Severity = InteractionSeverity.Moderate, Note = "Furosemide can raise blood glucose; monitor diabetic control." },
        new DrugInteraction { DrugAKey = "fluoxetine", DrugBKey = "tramadol", Severity = InteractionSeverity.Severe, Note = "Serotonin syndrome risk — avoid combination." },
        new DrugInteraction { DrugAKey = "fluoxetine", DrugBKey = "aspirin", Severity = InteractionSeverity.Moderate, Note = "Increased GI bleeding risk." },
        new DrugInteraction { DrugAKey = "ciprofloxacin", DrugBKey = "tramadol", Severity = InteractionSeverity.Moderate, Note = "Lower seizure threshold — caution in epilepsy." },
        new DrugInteraction { DrugAKey = "diazepam", DrugBKey = "morphine", Severity = InteractionSeverity.Severe, Note = "Profound CNS depression — avoid concurrent use." },
        new DrugInteraction { DrugAKey = "diazepam", DrugBKey = "tramadol", Severity = InteractionSeverity.Severe, Note = "CNS depression and respiratory depression risk." },
        new DrugInteraction { DrugAKey = "rifampicin", DrugBKey = "warfarin", Severity = InteractionSeverity.Severe, Note = "Rifampicin induces warfarin metabolism — INR drops; expect dose increase." },
        new DrugInteraction { DrugAKey = "atorvastatin", DrugBKey = "erythromycin", Severity = InteractionSeverity.Moderate, Note = "Macrolides inhibit statin metabolism — myopathy risk." },
        new DrugInteraction { DrugAKey = "amoxicillin", DrugBKey = "warfarin", Severity = InteractionSeverity.Moderate, Note = "Antibiotics can disrupt gut flora reducing vitamin K — INR may rise." }
    };

    private static IEnumerable<Payer> SeedPayers()
    {
        Payer P(string name, string code, PayerOrgType type, string? regNum = null, string? phone = null, string? email = null, string? claimsEmail = null, params PayerPlan[] plans)
        {
            var p = new Payer { Name = name, Code = code, OrgType = type, RegulatorRegistrationNumber = regNum, Phone = phone, Email = email, ClaimsDispatchEmail = claimsEmail };
            foreach (var pl in plans) p.Plans.Add(pl);
            return p;
        }
        PayerPlan Plan(string name, string code, decimal mult, decimal copay = 0, decimal cap = 0, bool preauth = false)
            => new PayerPlan { Name = name, Code = code, TariffMultiplier = mult, DefaultCopayPercent = copay, CapitationRatePerEnrolleeMonth = cap, RequiresPreAuthorization = preauth };

        return new[]
        {
            P("Out-of-pocket (cash/POS/transfer)", "OOP", PayerOrgType.OutOfPocket,
                plans: new[] { Plan("Self-pay", "OOP-SELF", 1.00m) }),

            P("National Health Insurance Authority", "NHIA", PayerOrgType.Nhia, regNum: "NHIA-MAIN",
                phone: "+234-9-461-2000", email: "info@nhia.gov.ng", claimsEmail: "claims@nhia.gov.ng",
                plans: new[]
                {
                    Plan("NHIA Formal Sector", "NHIA-FORMAL", 0.80m, copay: 10m, preauth: true),
                    Plan("NHIA BHCPF (PHC capitation)", "NHIA-BHCPF", 0.85m, cap: 750m),
                    Plan("NHIA Tertiary Institutions", "NHIA-TERT", 0.80m, copay: 10m, preauth: true)
                }),

            P("Hygeia HMO", "HYG", PayerOrgType.Hmo, regNum: "HMO/2003/001",
                phone: "+234-1-462-9999", email: "info@hygeiahmo.com", claimsEmail: "claims@hygeiahmo.com",
                plans: new[]
                {
                    Plan("Hygeia Standard", "HYG-STD", 0.85m, copay: 10m, preauth: true),
                    Plan("Hygeia Premium", "HYG-PREMIUM", 0.95m, copay: 0, preauth: false)
                }),

            P("Reliance HMO", "REL", PayerOrgType.Hmo, regNum: "HMO/2007/045",
                phone: "+234-1-700-6677", email: "info@reliancehmo.com", claimsEmail: "claims@reliancehmo.com",
                plans: new[] { Plan("Reliance Standard", "REL-STD", 0.82m, copay: 10m, preauth: true) }),

            P("AXA Mansard Health", "AXAM", PayerOrgType.Hmo, regNum: "HMO/2014/099",
                phone: "+234-1-448-0480", email: "info@axamansardhealth.com", claimsEmail: "claims@axamansardhealth.com",
                plans: new[]
                {
                    Plan("AXA Mansard Standard", "AXAM-STD", 0.85m, copay: 10m, preauth: true),
                    Plan("AXA Mansard Executive", "AXAM-EXEC", 0.95m, copay: 0, preauth: false)
                }),

            P("Avon Healthcare", "AVON", PayerOrgType.Hmo, regNum: "HMO/2007/032",
                phone: "+234-1-279-9696", email: "info@avonhealthcare.com", claimsEmail: "claims@avonhealthcare.com",
                plans: new[] { Plan("Avon Standard", "AVON-STD", 0.83m, copay: 10m, preauth: true) }),

            P("Leadway Health", "LEAD", PayerOrgType.Hmo, regNum: "HMO/2018/120",
                phone: "+234-1-280-8800", email: "info@leadwayhealth.com", claimsEmail: "claims@leadwayhealth.com",
                plans: new[] { Plan("Leadway Standard", "LEAD-STD", 0.83m, copay: 10m, preauth: true) }),

            P("Lagos State Health Insurance Scheme", "LASHMA", PayerOrgType.StateInsurance, regNum: "LASHMA-2018",
                phone: "+234-1-454-5566", email: "info@lashma.lg.gov.ng", claimsEmail: "claims@lashma.lg.gov.ng",
                plans: new[] { Plan("LASHMA Equity (formal)", "LASHMA-FORMAL", 0.78m, copay: 5m, preauth: true) })
        };
    }

    private static IEnumerable<Supplier> SeedSuppliers() => new[]
    {
        new Supplier { Name = "Emzor Pharmaceuticals", Code = "EMZOR", ContactPerson = "Sales Desk", Phone = "+234-1-461-2222", Email = "sales@emzorpharma.com", Address = "Plot 3C Block A Aswani Industrial, Lagos", RcNumber = "RC-12345", PaymentTerms = "Net 30", LeadTimeDays = 5 },
        new Supplier { Name = "Fidson Healthcare", Code = "FIDSON", ContactPerson = "Account Manager", Phone = "+234-1-461-3000", Email = "orders@fidson.com", Address = "268 Ikorodu Road, Obanikoro, Lagos", RcNumber = "RC-23456", PaymentTerms = "Net 45", LeadTimeDays = 7 },
        new Supplier { Name = "May & Baker Nigeria", Code = "MAYBAKER", Phone = "+234-1-461-4000", Email = "ng-orders@may-baker.com", Address = "3/5 Sapara St, Ikeja, Lagos", RcNumber = "RC-34567", PaymentTerms = "Net 30", LeadTimeDays = 5 },
        new Supplier { Name = "BD Biosciences (Becton Dickinson)", Code = "BD", Phone = "+234-1-461-5000", Email = "bd-ng@bd.com", Address = "Lekki Phase 1, Lagos", RcNumber = "RC-45678", PaymentTerms = "Net 60", LeadTimeDays = 14 },
        new Supplier { Name = "Roche Diagnostics Nigeria", Code = "ROCHE", Phone = "+234-1-461-6000", Email = "ng.diagnostics@roche.com", Address = "Victoria Island, Lagos", RcNumber = "RC-56789", PaymentTerms = "Net 60", LeadTimeDays = 21 },
        new Supplier { Name = "Mopson Pharmacy Ltd", Code = "MOPSON", ContactPerson = "Bola Adeola", Phone = "+234-803-444-5566", Email = "info@mopsonpharma.com", Address = "5 Allen Avenue, Ikeja", RcNumber = "RC-67890", PaymentTerms = "Net 14", LeadTimeDays = 3 },
        new Supplier { Name = "Megalife Sciences", Code = "MEGALIFE", Phone = "+234-1-461-7777", Email = "supply@megalife.ng", Address = "Apapa, Lagos", RcNumber = "RC-78901", PaymentTerms = "Net 30", LeadTimeDays = 7 }
    };

    private static IEnumerable<InventoryItem> SeedInventoryItems() => new[]
    {
        // Consumables
        new InventoryItem { Name = "Disposable nitrile gloves (medium)", Code = "GLV-NIT-M", Category = InventoryCategory.Consumable, UnitOfIssue = "box of 100", UnitPrice = 4500m, ReorderLevel = 20, IsExpiringTracked = false },
        new InventoryItem { Name = "Disposable nitrile gloves (large)", Code = "GLV-NIT-L", Category = InventoryCategory.Consumable, UnitOfIssue = "box of 100", UnitPrice = 4500m, ReorderLevel = 20, IsExpiringTracked = false },
        new InventoryItem { Name = "Surgical face mask (3-ply)", Code = "MSK-3PLY", Category = InventoryCategory.Consumable, UnitOfIssue = "box of 50", UnitPrice = 2500m, ReorderLevel = 30, IsExpiringTracked = false },
        new InventoryItem { Name = "Sterile gauze swab 7.5×7.5cm", Code = "GAUZE-7", Category = InventoryCategory.Consumable, UnitOfIssue = "pack of 100", UnitPrice = 3500m, ReorderLevel = 20 },
        new InventoryItem { Name = "Cotton wool roll (500g)", Code = "COT-500", Category = InventoryCategory.Consumable, UnitOfIssue = "roll", UnitPrice = 1200m, ReorderLevel = 30, IsExpiringTracked = false },
        new InventoryItem { Name = "IV cannula 18G", Code = "CAN-18G", Category = InventoryCategory.Consumable, UnitOfIssue = "each", UnitPrice = 250m, ReorderLevel = 200, Manufacturer = "BD" },
        new InventoryItem { Name = "IV cannula 20G", Code = "CAN-20G", Category = InventoryCategory.Consumable, UnitOfIssue = "each", UnitPrice = 250m, ReorderLevel = 200, Manufacturer = "BD" },
        new InventoryItem { Name = "Disposable syringe 5ml", Code = "SYR-5ML", Category = InventoryCategory.Consumable, UnitOfIssue = "each", UnitPrice = 60m, ReorderLevel = 500 },
        new InventoryItem { Name = "Disposable syringe 10ml", Code = "SYR-10ML", Category = InventoryCategory.Consumable, UnitOfIssue = "each", UnitPrice = 90m, ReorderLevel = 500 },
        new InventoryItem { Name = "Foley catheter 16Fr", Code = "FOLEY-16", Category = InventoryCategory.Consumable, UnitOfIssue = "each", UnitPrice = 1500m, ReorderLevel = 30 },
        new InventoryItem { Name = "Surgical blade #11", Code = "BLADE-11", Category = InventoryCategory.Consumable, UnitOfIssue = "each", UnitPrice = 200m, ReorderLevel = 100 },
        new InventoryItem { Name = "Suture · Vicryl 2-0", Code = "SUT-VIC-20", Category = InventoryCategory.Consumable, UnitOfIssue = "each", UnitPrice = 1800m, ReorderLevel = 50 },

        // Reagents
        new InventoryItem { Name = "FBC reagent kit", Code = "RGT-FBC", Category = InventoryCategory.Reagent, UnitOfIssue = "kit (200 tests)", UnitPrice = 75000m, ReorderLevel = 5, Manufacturer = "Roche" },
        new InventoryItem { Name = "Glucose test strips", Code = "RGT-GLU", Category = InventoryCategory.Reagent, UnitOfIssue = "box of 50", UnitPrice = 8500m, ReorderLevel = 20 },
        new InventoryItem { Name = "Malaria RDT kit", Code = "RGT-MRDT", Category = InventoryCategory.Reagent, UnitOfIssue = "box of 25", UnitPrice = 12500m, ReorderLevel = 30 },
        new InventoryItem { Name = "HIV rapid test kit (Determine)", Code = "RGT-HIV", Category = InventoryCategory.Reagent, UnitOfIssue = "box of 100", UnitPrice = 22000m, ReorderLevel = 10 },
        new InventoryItem { Name = "Pregnancy test strips (HCG)", Code = "RGT-HCG", Category = InventoryCategory.Reagent, UnitOfIssue = "box of 50", UnitPrice = 4500m, ReorderLevel = 20 },
        new InventoryItem { Name = "Urinalysis dipstick (10-parameter)", Code = "RGT-URN10", Category = InventoryCategory.Reagent, UnitOfIssue = "tube of 100", UnitPrice = 6500m, ReorderLevel = 20 },

        // Equipment (capital, longer lifecycle)
        new InventoryItem { Name = "Stethoscope (acoustic, dual-head)", Code = "EQP-STETH", Category = InventoryCategory.Equipment, UnitOfIssue = "each", UnitPrice = 18000m, ReorderLevel = 5, IsExpiringTracked = false },
        new InventoryItem { Name = "Digital BP machine (auto)", Code = "EQP-BP", Category = InventoryCategory.Equipment, UnitOfIssue = "each", UnitPrice = 32000m, ReorderLevel = 3, IsExpiringTracked = false },
        new InventoryItem { Name = "Pulse oximeter (fingertip)", Code = "EQP-SPO2", Category = InventoryCategory.Equipment, UnitOfIssue = "each", UnitPrice = 12000m, ReorderLevel = 5, IsExpiringTracked = false },
        new InventoryItem { Name = "Glucometer (handheld)", Code = "EQP-GLUC", Category = InventoryCategory.Equipment, UnitOfIssue = "each", UnitPrice = 15000m, ReorderLevel = 3, IsExpiringTracked = false },

        // Linen / cleaning
        new InventoryItem { Name = "Bedsheet (cotton, single)", Code = "LIN-SHT", Category = InventoryCategory.Linen, UnitOfIssue = "each", UnitPrice = 3500m, ReorderLevel = 20, IsExpiringTracked = false },
        new InventoryItem { Name = "Patient gown", Code = "LIN-GWN", Category = InventoryCategory.Linen, UnitOfIssue = "each", UnitPrice = 4500m, ReorderLevel = 20, IsExpiringTracked = false },
        new InventoryItem { Name = "Hand sanitiser 500ml", Code = "CLN-SAN", Category = InventoryCategory.Cleaning, UnitOfIssue = "bottle", UnitPrice = 1800m, ReorderLevel = 50 },
        new InventoryItem { Name = "Hypochlorite disinfectant 1L", Code = "CLN-HYP", Category = InventoryCategory.Cleaning, UnitOfIssue = "bottle", UnitPrice = 1500m, ReorderLevel = 30 }
    };

    private static void SeedVaccineCatalog(ApplicationDbContext db)
    {
        // Nigerian National Programme on Immunisation (NPI) routine schedule
        var bcg = new Vaccine { Code = "BCG", Name = "BCG", Description = "Tuberculosis (intradermal, right upper arm)", Route = VaccineRoute.IntraDermal, Site = "Right upper arm", SortOrder = 10 };
        var opv = new Vaccine { Code = "OPV", Name = "Oral Polio Vaccine", Description = "Oral polio drops", Route = VaccineRoute.Oral, Site = "Oral", SortOrder = 20 };
        var hepB = new Vaccine { Code = "HepB", Name = "Hepatitis B (birth dose)", Description = "Hepatitis B birth-dose monovalent", Route = VaccineRoute.IntraMuscular, Site = "Anterolateral thigh", SortOrder = 30 };
        var penta = new Vaccine { Code = "Penta", Name = "Pentavalent (DPT-HepB-Hib)", Description = "Diphtheria-Pertussis-Tetanus + HepB + Hib", Route = VaccineRoute.IntraMuscular, Site = "Anterolateral thigh", SortOrder = 40 };
        var pcv = new Vaccine { Code = "PCV", Name = "Pneumococcal (PCV-10/13)", Description = "Pneumococcal conjugate vaccine", Route = VaccineRoute.IntraMuscular, Site = "Anterolateral thigh", SortOrder = 50 };
        var rota = new Vaccine { Code = "Rota", Name = "Rotavirus", Description = "Oral rotavirus vaccine", Route = VaccineRoute.Oral, Site = "Oral", SortOrder = 60 };
        var ipv = new Vaccine { Code = "IPV", Name = "Inactivated Polio (IPV)", Description = "Inactivated polio injectable", Route = VaccineRoute.IntraMuscular, Site = "Anterolateral thigh", SortOrder = 70 };
        var measles = new Vaccine { Code = "Measles", Name = "Measles / MR", Description = "Measles or measles-rubella", Route = VaccineRoute.SubCutaneous, Site = "Right upper arm", SortOrder = 80 };
        var yf = new Vaccine { Code = "YF", Name = "Yellow Fever", Description = "Yellow fever live attenuated", Route = VaccineRoute.SubCutaneous, Site = "Left upper arm", SortOrder = 90 };
        var menA = new Vaccine { Code = "MenA", Name = "Meningitis A", Description = "MenAfriVac (meningococcal A conjugate)", Route = VaccineRoute.IntraMuscular, Site = "Left upper arm", SortOrder = 100 };
        var hpv = new Vaccine { Code = "HPV", Name = "Human Papillomavirus", Description = "HPV (girls 9-14y)", Route = VaccineRoute.IntraMuscular, Site = "Deltoid", SortOrder = 110 };
        var td = new Vaccine { Code = "Td", Name = "Tetanus-Diphtheria (Td)", Description = "Tetanus toxoid (booster)", Route = VaccineRoute.IntraMuscular, Site = "Deltoid", SortOrder = 120 };

        db.Vaccines.AddRange(bcg, opv, hepB, penta, pcv, rota, ipv, measles, yf, menA, hpv, td);

        // Schedule (weeks of age)
        db.VaccineSchedules.AddRange(
            new VaccineSchedule { Vaccine = bcg, DoseLabel = "BCG", RecommendedAgeWeeks = 0, SortOrder = 1 },
            new VaccineSchedule { Vaccine = opv, DoseLabel = "OPV0", RecommendedAgeWeeks = 0, SortOrder = 1 },
            new VaccineSchedule { Vaccine = opv, DoseLabel = "OPV1", RecommendedAgeWeeks = 6, SortOrder = 2 },
            new VaccineSchedule { Vaccine = opv, DoseLabel = "OPV2", RecommendedAgeWeeks = 10, SortOrder = 3 },
            new VaccineSchedule { Vaccine = opv, DoseLabel = "OPV3", RecommendedAgeWeeks = 14, SortOrder = 4 },
            new VaccineSchedule { Vaccine = hepB, DoseLabel = "HepB-Birth", RecommendedAgeWeeks = 0, SortOrder = 1 },
            new VaccineSchedule { Vaccine = penta, DoseLabel = "Penta1", RecommendedAgeWeeks = 6, SortOrder = 1 },
            new VaccineSchedule { Vaccine = penta, DoseLabel = "Penta2", RecommendedAgeWeeks = 10, SortOrder = 2 },
            new VaccineSchedule { Vaccine = penta, DoseLabel = "Penta3", RecommendedAgeWeeks = 14, SortOrder = 3 },
            new VaccineSchedule { Vaccine = pcv, DoseLabel = "PCV1", RecommendedAgeWeeks = 6, SortOrder = 1 },
            new VaccineSchedule { Vaccine = pcv, DoseLabel = "PCV2", RecommendedAgeWeeks = 10, SortOrder = 2 },
            new VaccineSchedule { Vaccine = pcv, DoseLabel = "PCV3", RecommendedAgeWeeks = 14, SortOrder = 3 },
            new VaccineSchedule { Vaccine = rota, DoseLabel = "Rota1", RecommendedAgeWeeks = 6, SortOrder = 1 },
            new VaccineSchedule { Vaccine = rota, DoseLabel = "Rota2", RecommendedAgeWeeks = 10, SortOrder = 2 },
            new VaccineSchedule { Vaccine = ipv, DoseLabel = "IPV", RecommendedAgeWeeks = 14, SortOrder = 1 },
            new VaccineSchedule { Vaccine = measles, DoseLabel = "Measles1", RecommendedAgeWeeks = 39, SortOrder = 1 },  // 9 months
            new VaccineSchedule { Vaccine = measles, DoseLabel = "Measles2", RecommendedAgeWeeks = 65, SortOrder = 2 },  // 15 months
            new VaccineSchedule { Vaccine = yf, DoseLabel = "YF", RecommendedAgeWeeks = 39, SortOrder = 1 },             // 9 months
            new VaccineSchedule { Vaccine = menA, DoseLabel = "MenA", RecommendedAgeWeeks = 65, SortOrder = 1 },         // 15 months
            new VaccineSchedule { Vaccine = hpv, DoseLabel = "HPV1", RecommendedAgeWeeks = 468, SortOrder = 1, Notes = "9–14 yo girls" }, // ~9y
            new VaccineSchedule { Vaccine = hpv, DoseLabel = "HPV2", RecommendedAgeWeeks = 494, SortOrder = 2, Notes = "6 months after HPV1" }
        );
    }

    private static IEnumerable<NotifiableDisease> SeedNotifiableDiseases() => new[]
    {
        // NCDC priority diseases — Nigeria National IDSR Technical Guidelines (3rd ed)
        new NotifiableDisease { Code = "AFP", Name = "Acute Flaccid Paralysis (Polio)", Category = DiseaseCategory.EradicationTarget, Window = NotificationWindow.Immediate, SortOrder = 10, CaseDefinition = "Sudden onset of weakness or paralysis in any part of the body, in a child < 15y" },
        new NotifiableDisease { Code = "MEAS", Name = "Measles", Category = DiseaseCategory.EpidemicProne, Window = NotificationWindow.Immediate, SortOrder = 20, CaseDefinition = "Generalized maculopapular rash + fever ≥38°C + cough/coryza/conjunctivitis" },
        new NotifiableDisease { Code = "CHOL", Name = "Cholera", Category = DiseaseCategory.EpidemicProne, Window = NotificationWindow.Immediate, SortOrder = 30, CaseDefinition = "Acute watery diarrhoea ± vomiting in a person ≥5y" },
        new NotifiableDisease { Code = "LASF", Name = "Lassa Fever", Category = DiseaseCategory.EpidemicProne, Window = NotificationWindow.Immediate, SortOrder = 40, CaseDefinition = "Fever ≥38°C + headache + sore throat or general weakness with no other obvious cause" },
        new NotifiableDisease { Code = "YF", Name = "Yellow Fever", Category = DiseaseCategory.EpidemicProne, Window = NotificationWindow.Immediate, SortOrder = 50, CaseDefinition = "Acute febrile illness with jaundice within 2 weeks of onset" },
        new NotifiableDisease { Code = "MENI", Name = "Meningitis", Category = DiseaseCategory.EpidemicProne, Window = NotificationWindow.Immediate, SortOrder = 60, CaseDefinition = "Sudden fever (>38.5°C) + neck stiffness ± altered consciousness" },
        new NotifiableDisease { Code = "COVID", Name = "COVID-19", Category = DiseaseCategory.EpidemicProne, Window = NotificationWindow.Immediate, SortOrder = 70, CaseDefinition = "Acute respiratory illness with positive SARS-CoV-2 test" },
        new NotifiableDisease { Code = "MPOX", Name = "Mpox (Monkeypox)", Category = DiseaseCategory.EpidemicProne, Window = NotificationWindow.Immediate, SortOrder = 80, CaseDefinition = "Acute illness with fever, intense headache, lymphadenopathy followed by rash" },
        new NotifiableDisease { Code = "DIPH", Name = "Diphtheria", Category = DiseaseCategory.EpidemicProne, Window = NotificationWindow.Immediate, SortOrder = 90, CaseDefinition = "Pharyngitis or tonsillitis with greyish adherent membrane" },
        new NotifiableDisease { Code = "VHF", Name = "Other Viral Haemorrhagic Fevers", Category = DiseaseCategory.EpidemicProne, Window = NotificationWindow.Immediate, SortOrder = 100, CaseDefinition = "Fever ≥38°C with bleeding from any site or severe systemic illness" },
        new NotifiableDisease { Code = "MAL-S", Name = "Severe Malaria", Category = DiseaseCategory.OutbreakProne, Window = NotificationWindow.Weekly, SortOrder = 110, CaseDefinition = "Fever + parasitaemia + clinical features of severe malaria" },
        new NotifiableDisease { Code = "TB", Name = "Tuberculosis (smear/PCR positive)", Category = DiseaseCategory.OtherPriority, Window = NotificationWindow.Monthly, SortOrder = 120 },
        new NotifiableDisease { Code = "HIV", Name = "HIV (new diagnosis)", Category = DiseaseCategory.OtherPriority, Window = NotificationWindow.Monthly, SortOrder = 130 },
        new NotifiableDisease { Code = "PERT", Name = "Pertussis (Whooping cough)", Category = DiseaseCategory.OutbreakProne, Window = NotificationWindow.Weekly, SortOrder = 140 },
        new NotifiableDisease { Code = "RUBE", Name = "Rubella", Category = DiseaseCategory.EliminationTarget, Window = NotificationWindow.Weekly, SortOrder = 150 },
        new NotifiableDisease { Code = "TET", Name = "Neonatal Tetanus", Category = DiseaseCategory.EliminationTarget, Window = NotificationWindow.Immediate, SortOrder = 160, CaseDefinition = "Newborn (0-28d) with normal cry & suck for first 2d, then unable to suck + spasms" },
        new NotifiableDisease { Code = "GUW", Name = "Guinea Worm (Dracunculiasis)", Category = DiseaseCategory.EradicationTarget, Window = NotificationWindow.Immediate, SortOrder = 170 },
        new NotifiableDisease { Code = "ANTH", Name = "Anthrax", Category = DiseaseCategory.EpidemicProne, Window = NotificationWindow.Immediate, SortOrder = 180 },
        new NotifiableDisease { Code = "RABI", Name = "Rabies (human)", Category = DiseaseCategory.EpidemicProne, Window = NotificationWindow.Immediate, SortOrder = 190 },
        new NotifiableDisease { Code = "DENG", Name = "Dengue Fever", Category = DiseaseCategory.OutbreakProne, Window = NotificationWindow.Weekly, SortOrder = 200 },
        new NotifiableDisease { Code = "AFI", Name = "Acute Febrile Illness (unknown cause)", Category = DiseaseCategory.OutbreakProne, Window = NotificationWindow.Weekly, SortOrder = 210 },
        new NotifiableDisease { Code = "MD", Name = "Maternal Death", Category = DiseaseCategory.OtherPriority, Window = NotificationWindow.Immediate, SortOrder = 220 },
        new NotifiableDisease { Code = "ND", Name = "Neonatal Death", Category = DiseaseCategory.OtherPriority, Window = NotificationWindow.Immediate, SortOrder = 230 },
        new NotifiableDisease { Code = "AEFI", Name = "Adverse Event Following Immunization", Category = DiseaseCategory.OtherPriority, Window = NotificationWindow.Immediate, SortOrder = 240 }
    };

    private static async Task SeedDefaultRolePermissionsAsync(ApplicationDbContext db, RoleManager<IdentityRole> roleManager)
    {
        // Default permission set per role. Only seeds for roles that have NO rows yet — so admin's
        // changes through the UI are never overwritten by a restart.
        var defaults = new Dictionary<string, string[]>
        {
            [Roles.SystemAdministrator] = Permissions.All, // full access
            [Roles.MedicalDirector] = Permissions.All,     // full access
            [Roles.ChiefExecutive] = new[]
            {
                Permissions.PatientsRead, Permissions.AppointmentsRead, Permissions.QueueRead,
                Permissions.AnalyticsView, Permissions.NhmisGenerate, Permissions.NhmisSubmit, Permissions.IdsrReport,
                Permissions.HrRead, Permissions.AuditView
            },
            [Roles.ChiefFinancialOfficer] = new[]
            {
                Permissions.BillsRead, Permissions.ClaimsManage, Permissions.PayersManage, Permissions.AnalyticsView,
                Permissions.PurchaseOrderManage, Permissions.AuditView
            },
            [Roles.ChiefNursingOfficer] = new[]
            {
                Permissions.PatientsRead, Permissions.QueueRead, Permissions.QueueServe,
                Permissions.AdmissionsManage, Permissions.WardsManage, Permissions.IcuChart, Permissions.RosterManage,
                Permissions.LeaveDecide, Permissions.AnalyticsView, Permissions.HrRead
            },
            [Roles.HrOfficer] = new[]
            {
                Permissions.HrRead, Permissions.HrEdit, Permissions.RosterManage, Permissions.LeaveDecide, Permissions.LeaveRequest,
                Permissions.PatientsRead, Permissions.StaffManage
            },
            [Roles.Consultant] = new[]
            {
                Permissions.PatientsRead, Permissions.QueueRead, Permissions.QueueServe,
                Permissions.EncountersStart, Permissions.EncountersWrite, Permissions.EncountersSign,
                Permissions.OrdersLab, Permissions.OrdersImaging, Permissions.OrdersProcedure, Permissions.OrdersPrescribe,
                Permissions.LabRead, Permissions.ImagingRead,
                Permissions.AdmissionsManage, Permissions.TheatreSchedule, Permissions.TheatreOperate, Permissions.IcuChart,
                Permissions.AncManage, Permissions.DeliveryRecord, Permissions.PaedsManage, Permissions.ImmunizationAdminister,
                Permissions.TelemedicineClinician, Permissions.AnalyticsView, Permissions.LeaveRequest,
                Permissions.IdsrReport,
                Permissions.AiAssist, Permissions.AiLabInterpret, Permissions.AiDifferential, Permissions.AiDischargeDraft
            },
            [Roles.Doctor] = new[]
            {
                Permissions.PatientsRead, Permissions.QueueRead, Permissions.QueueServe,
                Permissions.EncountersStart, Permissions.EncountersWrite, Permissions.EncountersSign,
                Permissions.OrdersLab, Permissions.OrdersImaging, Permissions.OrdersProcedure, Permissions.OrdersPrescribe,
                Permissions.LabRead, Permissions.ImagingRead,
                Permissions.AdmissionsManage, Permissions.TheatreSchedule, Permissions.IcuChart,
                Permissions.PaedsManage, Permissions.ImmunizationAdminister,
                Permissions.TelemedicineClinician, Permissions.LeaveRequest, Permissions.IdsrReport,
                Permissions.AiAssist, Permissions.AiLabInterpret, Permissions.AiDifferential, Permissions.AiDischargeDraft
            },
            [Roles.MedicalOfficer] = new[]
            {
                Permissions.PatientsRead, Permissions.QueueRead, Permissions.QueueServe,
                Permissions.EncountersStart, Permissions.EncountersWrite,
                Permissions.OrdersLab, Permissions.OrdersImaging, Permissions.OrdersPrescribe,
                Permissions.LabRead, Permissions.ImagingRead,
                Permissions.AdmissionsManage, Permissions.TelemedicineClinician, Permissions.LeaveRequest, Permissions.IdsrReport,
                Permissions.AiAssist, Permissions.AiLabInterpret, Permissions.AiDifferential, Permissions.AiDischargeDraft
            },
            [Roles.Nurse] = new[]
            {
                Permissions.PatientsRead, Permissions.QueueRead, Permissions.QueueCheckIn, Permissions.AppointmentsCheckIn,
                Permissions.TriageCreate, Permissions.EmergencyBoardRead,
                Permissions.AdmissionsManage, Permissions.WardsManage, Permissions.IcuChart, Permissions.ImmunizationAdminister,
                Permissions.LeaveRequest
            },
            [Roles.Midwife] = new[]
            {
                Permissions.PatientsRead, Permissions.QueueRead, Permissions.AppointmentsCheckIn,
                Permissions.AncManage, Permissions.DeliveryRecord, Permissions.PaedsManage, Permissions.ImmunizationAdminister,
                Permissions.LeaveRequest
            },
            [Roles.Pharmacist] = new[]
            {
                Permissions.PatientsRead,
                Permissions.PharmacyDispense, Permissions.PharmacyControlled, Permissions.PharmacyStock,
                Permissions.PurchaseOrderManage, Permissions.GrnReceive, Permissions.InventoryRead,
                Permissions.LeaveRequest
            },
            [Roles.PharmacyTechnician] = new[]
            {
                Permissions.PatientsRead, Permissions.PharmacyDispense, Permissions.PharmacyStock, Permissions.LeaveRequest
            },
            [Roles.LabScientist] = new[]
            {
                Permissions.PatientsRead, Permissions.LabRead, Permissions.LabPerform, Permissions.LabAuthorize,
                Permissions.BloodBankManage, Permissions.BloodBankCrossMatch, Permissions.LeaveRequest,
                Permissions.AiAssist, Permissions.AiLabInterpret
            },
            [Roles.LabTechnician] = new[]
            {
                Permissions.PatientsRead, Permissions.LabRead, Permissions.LabPerform, Permissions.LeaveRequest
            },
            [Roles.Radiographer] = new[]
            {
                Permissions.PatientsRead, Permissions.ImagingRead, Permissions.ImagingPerform, Permissions.ImagingReport, Permissions.LeaveRequest
            },
            [Roles.Physiotherapist] = new[]
            {
                Permissions.PatientsRead, Permissions.AlliedSession, Permissions.LeaveRequest
            },
            [Roles.Receptionist] = new[]
            {
                Permissions.PatientsRead, Permissions.PatientsRegister, Permissions.PatientsEdit,
                Permissions.AppointmentsRead, Permissions.AppointmentsBook, Permissions.AppointmentsCheckIn,
                Permissions.QueueRead, Permissions.QueueCheckIn, Permissions.LeaveRequest
            },
            [Roles.RecordsOfficer] = new[]
            {
                Permissions.PatientsRead, Permissions.PatientsRegister, Permissions.PatientsEdit, Permissions.PatientsMerge,
                Permissions.AppointmentsRead, Permissions.AppointmentsBook, Permissions.AppointmentsCheckIn,
                Permissions.QueueRead, Permissions.QueueCheckIn, Permissions.MortuaryManage, Permissions.LeaveRequest
            },
            [Roles.TriageClerk] = new[]
            {
                Permissions.PatientsRead, Permissions.PatientsRegister,
                Permissions.QueueRead, Permissions.QueueCheckIn,
                Permissions.TriageCreate, Permissions.EmergencyBoardRead, Permissions.LeaveRequest
            },
            [Roles.Cashier] = new[]
            {
                Permissions.PatientsRead, Permissions.BillsRead, Permissions.BillsBuild, Permissions.BillsPaymentRecord,
                Permissions.CashierShiftManage, Permissions.LeaveRequest
            },
            [Roles.Accountant] = new[]
            {
                Permissions.BillsRead, Permissions.BillsDiscount, Permissions.ClaimsManage, Permissions.PayersManage,
                Permissions.PurchaseOrderManage, Permissions.AnalyticsView, Permissions.LeaveRequest
            },
            [Roles.ClaimsOfficer] = new[]
            {
                Permissions.PatientsRead, Permissions.ClaimsManage, Permissions.PayersManage, Permissions.LeaveRequest
            },
            [Roles.ProcurementOfficer] = new[]
            {
                Permissions.PurchaseOrderManage, Permissions.GrnReceive, Permissions.InventoryRead, Permissions.LeaveRequest
            },
            [Roles.StoreOfficer] = new[]
            {
                Permissions.PurchaseOrderManage, Permissions.GrnReceive, Permissions.InventoryRead, Permissions.StockTakeManage, Permissions.LeaveRequest
            },
            [Roles.BiomedicalEngineer] = new[]
            {
                Permissions.InventoryRead, Permissions.StockTakeManage, Permissions.LeaveRequest
            },
            [Roles.PublicHealthOfficer] = new[]
            {
                Permissions.PatientsRead, Permissions.IdsrReport, Permissions.IdsrNotify,
                Permissions.NhmisGenerate, Permissions.NhmisSubmit, Permissions.AnalyticsView,
                Permissions.ImmunizationAdminister, Permissions.PaedsManage, Permissions.LeaveRequest
            }
        };

        foreach (var (roleName, perms) in defaults)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null) continue;
            // Only seed if this role has no permissions assigned yet (idempotent).
            var hasAny = await db.RolePermissions.AnyAsync(p => p.RoleId == role.Id);
            if (hasAny) continue;
            foreach (var perm in perms.Distinct())
            {
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    Permission = perm,
                    GrantedAt = DateTime.UtcNow
                });
            }
        }
        await db.SaveChangesAsync();
    }
}
