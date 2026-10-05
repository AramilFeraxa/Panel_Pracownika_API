using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PanelPracownika.Controllers;
using PanelPracownika.Data;
using PanelPracownika.Models;
using PanelPracownika.Services;

var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
using var db = new AppDbContext(options);
db.Users.AddRange(new Login { Id = 1, Username = "admin", Name = "Admin", Surname = "Test", Password = "test", IsAdmin = true },
    new Login { Id = 2, Username = "employee", Name = "Employee", Surname = "Test", Password = "test" });
db.UserSalaries.AddRange(new UserSalary { UserId = 1, ContractType = "Umowa zlecenie", HourlyRate = 40 },
    new UserSalary { UserId = 2, ContractType = "Umowa o prace", MonthlySalary = 5000 });
for (var i = 1; i <= 20; i++)
{
    var work = new WorkTime { UserId = 1, Date = new DateTime(2026, 10, i), StartTime = "09:00", EndTime = "17:00" };
    work.SetTotal(TimeSpan.FromHours(9), TimeSpan.FromHours(17));
    db.WorkTimes.Add(work);
}
await db.SaveChangesAsync();
ControllerContext Context(int id) => new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "test")) } };
var admin = new AdminController(db, null!) { ControllerContext = Context(1) };
var controller = new SalaryController(db) { ControllerContext = Context(1) };
var count = 0;
void Check(bool valid, string name) { if (!valid) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); count++; }
var month = new SalaryController.GenerateSalaryDto { Year = 2026, Month = 10 };
Check(await controller.GenerateSalary(month) is OkObjectResult, "legacy hourly salary generation succeeds");
var record = db.SalaryRecords.Single(r => r.UserId == 1);
Check(record.ExpectedAmount == 6400 && record.Breakdown == null, "users without second contract retain original amount and no breakdown");
Check(await controller.PreviewSalary(2026, 10) is NoContentResult, "no new preview for users without second contract");
var enabled = new UpdateUserSalaryDto { ContractType = "Umowa zlecenie", HourlyRate = 40, HasSecondaryContract = true, SecondaryHourlyRate = 60, SecondaryMonthlyHours = 32 };
Check(await admin.UpdateUserSalary(1, enabled) is OkObjectResult, "admin can enable second contract with custom rate and hours");
var preview = (await controller.PreviewSalary(2026, 10) as OkObjectResult)?.Value as SalaryBreakdown;
Check(preview is { TotalHours: 160, PrimaryHours: 128, SecondaryHours: 32, PrimaryAmount: 5120, SecondaryAmount: 1920, ExpectedAmount: 7040 }, "160 hours split into 128 at 40 and 32 at 60");
Check(db.SalaryRecords.Single().ExpectedAmount == 6400 && db.WorkTimes.Sum(w => w.Total) == 160, "preview changes neither payroll history nor work hours");
await controller.GenerateSalary(month);
Check(record.ExpectedAmount == 7040 && record.Breakdown?.ExpectedAmount == preview?.ExpectedAmount, "generation matches preview and saves historical breakdown");
var savedDetails = record.CalculationDetails;
var snapshot = new SalaryRecord { CalculationDetails = savedDetails };
Check(snapshot.Breakdown is { SecondaryHourlyRate: 60, SecondaryHours: 32 }, "breakdown survives storage round-trip");
var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web));
Check(json.Contains("\"breakdown\"") && !json.Contains("calculationDetails"), "history exposes structured breakdown without internal JSON field");
enabled.SecondaryHourlyRate = 80;
await admin.UpdateUserSalary(1, enabled);
Check(record.CalculationDetails == savedDetails && record.ExpectedAmount == 7040, "changing rates preserves previous monthly calculation");
await controller.UpdateSalaryRecord(record.Id, new() { ReceivedAmount = 7100, HasBonus = true, Notes = "bonus" });
Check(record.CalculationDetails == savedDetails && record.ReceivedAmount == 7100, "receipt and note edits preserve historical breakdown");
Check(await admin.UpdateUserSalary(1, new() { ContractType = "Umowa zlecenie", HourlyRate = 45 }) is OkObjectResult && db.UserSalaries.Single(s => s.UserId == 1).HasSecondaryContract, "legacy client omitting optional fields preserves second contract");
var settings = db.UserSalaries.Single(s => s.UserId == 1);
Check(SalaryCalculation.CalculateSecondaryContract(settings, 32) is { PrimaryHours: 0, SecondaryHours: 32 }, "exact monthly quota leaves zero main hours");
Check(SalaryCalculation.CalculateSecondaryContract(settings, 20) is { PrimaryHours: 0, SecondaryHours: 20 }, "short month never creates negative main hours");
Check(SalaryCalculation.CalculateSecondaryContract(settings, 0) is { ExpectedAmount: 0, PrimaryHours: 0, SecondaryHours: 0 }, "empty month creates no phantom hours");
Check(SalaryCalculation.CalculateSecondaryContract(settings, 160.5) is { PrimaryHours: 128.5, SecondaryHours: 32 }, "fractional hours are preserved");
enabled.SecondaryHourlyRate = -1;
Check(await admin.UpdateUserSalary(1, enabled) is BadRequestObjectResult && settings.SecondaryHourlyRate == 80, "invalid rate rejected without modifying settings");
enabled.SecondaryHourlyRate = 60; enabled.SecondaryMonthlyHours = 0;
Check(await admin.UpdateUserSalary(1, enabled) is BadRequestObjectResult, "zero monthly quota rejected");
enabled.SecondaryMonthlyHours = 32; enabled.ContractType = "Umowa o prace";
Check(await admin.UpdateUserSalary(1, enabled) is BadRequestObjectResult && settings.ContractType == "Umowa zlecenie", "second contract restricted to hourly main contract");
admin.ControllerContext = Context(2);
Check(await admin.UpdateUserSalary(1, enabled) is ForbidResult, "ordinary employee cannot change contract settings");
admin.ControllerContext = Context(1);
Check(await admin.UpdateUserSalary(1, new() { ContractType = "Umowa zlecenie", HourlyRate = 40, HasSecondaryContract = false }) is OkObjectResult && !settings.HasSecondaryContract && settings.SecondaryHourlyRate == null, "disabling second contract clears its settings");
Check(record.CalculationDetails == savedDetails, "disabling second contract preserves historical breakdown");
await controller.GenerateSalary(month);
Check(record.ExpectedAmount == 6400 && record.Breakdown == null, "new calculations after disabling use the original formula");
controller.ControllerContext = Context(2);
await controller.GenerateSalary(month);
Check(db.SalaryRecords.Single(r => r.UserId == 2).ExpectedAmount == 5000 && db.SalaryRecords.Single(r => r.UserId == 2).Breakdown == null, "employment salary is unchanged");
Check(await controller.GenerateSalary(new() { Year = 2026, Month = 13 }) is BadRequestObjectResult, "invalid month rejected");
Check(await controller.UpdateSalaryRecord(record.Id, new() { ReceivedAmount = 0 }) is NotFoundObjectResult, "salary history remains restricted to its owner");
Console.WriteLine($"{count} salary checks passed.");
