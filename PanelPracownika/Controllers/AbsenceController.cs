using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PanelPracownika.Data;
using PanelPracownika.Models;
using System.Security.Claims;

namespace PanelPracownika.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AbsenceController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AbsenceController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetUserAbsences()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var dates = await _context.AbsenceDates
                .Where(d => d.UserId == userId)
                .Select(e => new
                {
                    date = e.Date.ToString("yyyy-MM-dd"),
                    type = e.Type
                })
                .ToListAsync();

            return Ok(dates);
        }

        [HttpPost]
        public async Task<IActionResult> AddAbsence([FromBody] AddAbsenceDto dto)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var date = DateTime.SpecifyKind(dto.Date.Date, DateTimeKind.Utc);

            if (date == default || string.IsNullOrEmpty(dto.Type)) return BadRequest("Niepoprawna data lub typ.");


            bool exists = await _context.AbsenceDates
                .AnyAsync(d => d.UserId == userId && d.Date.Date == date);

            if (exists)
                return Conflict("Wpis na ten dzień już istnieje.");

            var existingWorkTime = await _context.WorkTimes
                .FirstOrDefaultAsync(w => w.UserId == userId && w.Date.Date == date);

            if (existingWorkTime != null && existingWorkTime.Total > 0)
            {
                return Conflict("W tym dniu już istnieje wpis z godzinami pracy, nie można dodać delegacji.");
            }

            var record = new AbsenceDate
            {
                UserId = userId.Value,
                Date = date,
                Type = dto.Type
            };

            _context.AbsenceDates.Add(record);

            if (existingWorkTime == null)
            {
                var zeroTime = new WorkTime
                {
                    UserId = userId.Value,
                    Date = date,
                    StartTime = "00:00",
                    EndTime = "00:00",
                };

                zeroTime.SetTotal(TimeSpan.Zero, TimeSpan.Zero);
                _context.WorkTimes.Add(zeroTime);
            }

            await _context.SaveChangesAsync();
            return Ok();
        }


        [HttpDelete("{date}")]
        public async Task<IActionResult> DeleteAbsence(string date)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            if (!DateTime.TryParse(date, out var parsedDate))
                return BadRequest("Nieprawidłowy format daty.");

            var dateOnly = DateTime.SpecifyKind(parsedDate.Date, DateTimeKind.Utc);

            var record = await _context.AbsenceDates
                .FirstOrDefaultAsync(d => d.UserId == userId && d.Date.Date == dateOnly);

            if (record == null)
                return NotFound("Nie znaleziono wpisu.");

            _context.AbsenceDates.Remove(record);

            var workTime = await _context.WorkTimes
                .FirstOrDefaultAsync(w => w.UserId == userId && w.Date.Date == dateOnly && w.Total == 0);

            if (workTime != null)
            {
                _context.WorkTimes.Remove(workTime);
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdClaim?.Value, out int userId) ? userId : (int?)null;
        }
    }


    public class AddAbsenceDto
    {
        public DateTime Date { get; set; }
        public string Type { get; set; }
    }
}
