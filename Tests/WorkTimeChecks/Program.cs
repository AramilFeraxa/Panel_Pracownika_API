using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PanelPracownika.Controllers;
using PanelPracownika.Data;
using PanelPracownika.Models;
using PanelPracownika.Services;

// Uses an isolated in-memory store. Transaction rollback and MySQL locking require an integration environment.
var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString())
    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
using var db = new AppDbContext(options);
var controller = new WorkTimeController(db, null!, Options.Create(new EmailSettings()), NullLogger<WorkTimeController>.Instance);
var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "1") }, "test");
controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
var day = new DateTime(2026, 10, 5);
var count = 0;
void Check(bool valid, string name)
{
    if (!valid) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    count++;
}
WorkPeriodDto Period(string start, string end, bool remote = false) => new() { StartTime = start, EndTime = end, IsRemote = remote };
WorkTimePeriodsDto Batch(params WorkPeriodDto[] periods) => new() { Date = day, Periods = periods.ToList() };

Check((await controller.GetWorkTimes()).Result is OkObjectResult { Value: List<WorkTime> { Count: 0 } }, "empty history returns an array");
Check(await controller.PostWorkPeriods(Batch(Period("09:00", "16:00"), Period("18:00", "20:00", true))) is OkObjectResult, "batch save succeeds");
Check(db.WorkTimes.Count() == 2 && db.WorkTimes.Sum(w => w.Total) == 9 && db.WorkTimes.Single(w => w.StartTime == "18:00").IsRemote, "9 hours and per-period remote flag persisted");
Check(await controller.PostWorkPeriods(Batch(Period("15:00", "17:00"))) is ConflictObjectResult && db.WorkTimes.Count() == 2, "saved overlap rejected without adding rows");
Check(await controller.PostWorkPeriods(Batch(Period("07:00", "08:00"), Period("07:30", "08:30"))) is BadRequestObjectResult && db.WorkTimes.Count() == 2, "batch overlap rejected as a whole");
Check(await controller.PostWorkPeriods(Batch(Period("07:00", "08:00"), Period("21:00", "20:00"))) is BadRequestObjectResult && db.WorkTimes.Count() == 2, "invalid second period leaves first unsaved");
Check(await controller.PostWorkPeriods(Batch(Period("24:00", "25:00"))) is BadRequestObjectResult, "out-of-day hours rejected");
Check(await controller.PostWorkPeriods(Batch()) is BadRequestObjectResult, "empty batch rejected");
var second = db.WorkTimes.Single(w => w.StartTime == "18:00");
Check(await controller.PutWorkTime(second.Id, new() { Id = second.Id, Date = day, StartTime = "15:00", EndTime = "20:00" }) is ConflictObjectResult && second.StartTime == "18:00", "overlapping edit preserves original period");
Check(await controller.PutWorkTime(second.Id, new() { Id = second.Id, Date = day, StartTime = "18:00", EndTime = "21:00", IsRemote = true }) is NoContentResult && db.WorkTimes.Sum(w => w.Total) == 10, "edit recalculates total");
Check((await controller.PostWorkTime(new() { Date = day, StartTime = "16:00:00", EndTime = "18:00:00" })).Result is OkObjectResult && db.WorkTimes.Sum(w => w.Total) == 12, "legacy POST accepts adjacent periods and normalizes seconds");
Check(await controller.DeleteWorkTime(second.Id) is NoContentResult && db.WorkTimes.Sum(w => w.Total) == 9, "delete removes only selected period");
identity.RemoveClaim(identity.FindFirst(ClaimTypes.NameIdentifier)!);
identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "2"));
Check(await controller.PutWorkTime(db.WorkTimes.First().Id, new() { Id = db.WorkTimes.First().Id, Date = day, StartTime = "09:00", EndTime = "10:00" }) is NotFoundResult, "cannot edit another user's period");
Check((await controller.PostWorkTime(new() { Date = day, StartTime = "09:00", EndTime = "10:00" })).Result is OkObjectResult, "another user can work the same hours");
identity.RemoveClaim(identity.FindFirst(ClaimTypes.NameIdentifier)!);
identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "1"));
var absenceController = new AbsenceController(db) { ControllerContext = controller.ControllerContext };
var zero = new WorkTime { UserId = 1, Date = day, StartTime = "00:00", EndTime = "00:00" };
db.WorkTimes.Add(zero);
await db.SaveChangesAsync();
Check(await absenceController.AddAbsence(new() { Date = day, Type = "Wyjazd", Reason = "Wyjazd" }) is ConflictObjectResult, "absence checks all periods even with a zero-hour row");
db.AbsenceDates.Add(new AbsenceDate { UserId = 1, Date = day.AddDays(1), Type = "Wyjazd", Reason = "Wyjazd" });
await db.SaveChangesAsync();
Check(await controller.PostWorkPeriods(new() { Date = day.AddDays(1), Periods = new() { Period("09:00", "10:00") } }) is ConflictObjectResult, "work rejected on an absence day");
db.AbsenceDates.Add(new AbsenceDate { UserId = 1, Date = day, Type = "Wyjazd", Reason = "legacy" });
await db.SaveChangesAsync();
Check(await absenceController.DeleteAbsence("2026-10-05") is NoContentResult && db.WorkTimes.Where(w => w.UserId == 1).Sum(w => w.Total) == 9, "removing a legacy absence preserves actual work periods");
Console.WriteLine($"{count} checks passed.");
