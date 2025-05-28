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
    public class WorkTimeController : ControllerBase
    {
        private readonly AppDbContext _context;

        public WorkTimeController(AppDbContext context)
        {
            _context = context;
        }

        [Authorize]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<WorkTime>>> GetWorkTimes()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized("Invalid or missing user ID in token.");
            }

            var workTimes = await _context.WorkTimes
                .Where(w => w.UserId == userId)
                .OrderBy(w => w.Date)
                .ToListAsync();

            if (workTimes == null || workTimes.Count == 0)
            {
                return NotFound("No work times found for this user.");
            }

            return Ok(workTimes);
        }


        [HttpPost]
        public async Task<ActionResult<WorkTime>> PostWorkTime([FromBody] WorkTimeDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized("Invalid or missing user ID in token.");

            if (!TimeSpan.TryParse(dto.StartTime, out TimeSpan start) || !TimeSpan.TryParse(dto.EndTime, out TimeSpan end))
                return BadRequest("Invalid time format.");

            var workTime = new WorkTime
            {
                Date = dto.Date,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                UserId = userId
            };

            workTime.SetTotal(start, end);

            _context.WorkTimes.Add(workTime);
            await _context.SaveChangesAsync();

            return Ok(workTime);
        }



        [HttpPut("{id}")]
        public async Task<IActionResult> PutWorkTime(int id, [FromBody] WorkTimeDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized();

            if (dto.Id != id)
                return BadRequest("ID mismatch");

            var workTime = await _context.WorkTimes.FindAsync(id);
            if (workTime == null || workTime.UserId != userId)
                return NotFound();

            workTime.Date = dto.Date;
            workTime.StartTime = dto.StartTime;
            workTime.EndTime = dto.EndTime;

            if (!TimeSpan.TryParse(dto.StartTime, out TimeSpan start) || !TimeSpan.TryParse(dto.EndTime, out TimeSpan end))
                return BadRequest("Invalid time format.");

            workTime.SetTotal(start, end);

            _context.Entry(workTime).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return NoContent();
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteWorkTime(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized("Invalid or missing user ID in token.");
            }

            var workTime = await _context.WorkTimes.FindAsync(id);
            if (workTime == null || workTime.UserId != userId)
            {
                return NotFound("WorkTime not found or does not belong to the user.");
            }

            _context.WorkTimes.Remove(workTime);
            await _context.SaveChangesAsync();
            return NoContent();
        }

    }
}
