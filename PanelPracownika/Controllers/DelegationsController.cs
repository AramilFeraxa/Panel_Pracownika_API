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
    public class DelegationsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DelegationsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetUserDelegations()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var dates = await _context.DelegationDates
                .Where(d => d.UserId == userId)
                .Select(d => d.Date.ToString("yyyy-MM-dd"))
                .ToListAsync();

            return Ok(dates);
        }

        [HttpPost]
        public async Task<IActionResult> AddDelegation([FromBody] AddDelegationDto dto)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var date = DateTime.SpecifyKind(dto.Date.Date, DateTimeKind.Utc);

            if (date == default)
                return BadRequest("Niepoprawna data.");

            bool exists = await _context.DelegationDates
                .AnyAsync(d => d.UserId == userId && d.Date.Date == date);

            if (exists)
                return Conflict("Delegacja już istnieje.");

            var existingWorkTime = await _context.WorkTimes
                .FirstOrDefaultAsync(w => w.UserId == userId && w.Date.Date == date);

            if (existingWorkTime != null && existingWorkTime.Total > 0)
            {
                return Conflict("W tym dniu już istnieje wpis z godzinami pracy, nie można dodać delegacji.");
            }

            var record = new DelegationDate
            {
                UserId = userId.Value,
                Date = date
            };

            _context.DelegationDates.Add(record);

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
        public async Task<IActionResult> DeleteDelegation(string date)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            if (!DateTime.TryParse(date, out var parsedDate))
                return BadRequest("Nieprawidłowy format daty.");

            var dateOnly = DateTime.SpecifyKind(parsedDate.Date, DateTimeKind.Utc);

            var record = await _context.DelegationDates
                .FirstOrDefaultAsync(d => d.UserId == userId && d.Date.Date == dateOnly);

            if (record == null)
                return NotFound("Nie znaleziono delegacji.");

            _context.DelegationDates.Remove(record);

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

    public class AddDelegationDto
    {
        public DateTime Date { get; set; }
    }
}
