using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PanelPracownika.Data;
using PanelPracownika.Models;
using PanelPracownika.Services;
using System.Security.Claims;

namespace PanelPracownika.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class SalaryController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SalaryController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetSalaryProfile()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var salary = await _context.UserSalaries.FirstOrDefaultAsync(s => s.UserId == userId);
            if (salary == null) return NotFound("Brak danych o wynagrodzeniu.");

            return Ok(salary);
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetSalaryHistory()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var records = await _context.SalaryRecords
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.Year)
                .ThenByDescending(s => s.Month)
                .ToListAsync();

            return Ok(records);
        }

        [HttpGet("preview")]
        public async Task<IActionResult> PreviewSalary([FromQuery] int year, [FromQuery] int month)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            if (year < 1 || year > 9999 || month < 1 || month > 12)
                return BadRequest("Wybierz poprawny rok i miesiąc.");
            var salary = await _context.UserSalaries.FirstOrDefaultAsync(s => s.UserId == userId);
            if (salary == null) return NotFound("Brak danych o wynagrodzeniu.");
            if (!salary.HasSecondaryContract) return NoContent();
            var validation = SalaryCalculation.ValidateSecondaryContract(salary);
            if (validation != null) return BadRequest(validation);
            var hours = await _context.WorkTimes
                .Where(w => w.UserId == userId && w.Date.Year == year && w.Date.Month == month)
                .SumAsync(w => w.Total);
            return Ok(SalaryCalculation.CalculateSecondaryContract(salary, hours));
        }

        [HttpPost("generate")]
        public async Task<IActionResult> GenerateSalary([FromBody] GenerateSalaryDto dto)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            if (dto.Year < 1 || dto.Year > 9999 || dto.Month < 1 || dto.Month > 12)
                return BadRequest("Wybierz poprawny rok i miesiąc.");
            var salaryInfo = await _context.UserSalaries.FirstOrDefaultAsync(u => u.UserId == userId);
            if (salaryInfo == null)
                return NotFound("Nie znaleziono danych o umowie użytkownika.");

            double amount = 0;
            SalaryBreakdown? breakdown = null;
            var validation = SalaryCalculation.ValidateSecondaryContract(salaryInfo);
            if (validation != null) return BadRequest(validation);

            if (salaryInfo.ContractType == "Umowa zlecenie")
            {
                var totalHours = await _context.WorkTimes
                    .Where(w => w.UserId == userId && w.Date.Year == dto.Year && w.Date.Month == dto.Month)
                    .SumAsync(w => w.Total);

                if (salaryInfo.HasSecondaryContract)
                {
                    breakdown = SalaryCalculation.CalculateSecondaryContract(salaryInfo, totalHours);
                    amount = breakdown.ExpectedAmount;
                }
                else
                {
                    amount = (salaryInfo.HourlyRate ?? 0) * totalHours;
                }
            }
            else if (salaryInfo.ContractType == "Umowa o prace")
            {
                amount = salaryInfo.MonthlySalary ?? 0;
            }
            else
            {
                return BadRequest("Nieobsługiwany typ umowy.");
            }

            var existingRecord = await _context.SalaryRecords
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Year == dto.Year && s.Month == dto.Month);

            if (existingRecord != null)
            {
                existingRecord.ExpectedAmount = Math.Round(amount, 2);
                existingRecord.Breakdown = breakdown;
                existingRecord.ReceivedAmount = 0;
                existingRecord.IsConfirmed = false;
                existingRecord.HasBonus = false;
                existingRecord.Notes = null;
            }
            else
            {
                var record = new SalaryRecord
                {
                    UserId = userId.Value,
                    Year = dto.Year,
                    Month = dto.Month,
                    ExpectedAmount = Math.Round(amount, 2),
                    Breakdown = breakdown,
                    ReceivedAmount = 0,
                    IsConfirmed = false,
                    HasBonus = false,
                };

                _context.SalaryRecords.Add(record);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Wynagrodzenie wygenerowane lub zaktualizowane." });
        }


        [HttpPut("update/{id}")]
        public async Task<IActionResult> UpdateSalaryRecord(int id, [FromBody] UpdateSalaryRecordDto dto)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var record = await _context.SalaryRecords
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

            if (record == null)
                return NotFound("Nie znaleziono rekordu wypłaty.");

            record.ReceivedAmount = dto.ReceivedAmount;
            record.IsConfirmed = dto.IsConfirmed;
            record.HasBonus = dto.HasBonus;
            record.Notes = dto.Notes;

            await _context.SaveChangesAsync();
            return NoContent();
        }


        public class GenerateSalaryDto
        {
            public int Year { get; set; }
            public int Month { get; set; }
        }

        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdClaim?.Value, out int userId) ? userId : (int?)null;
        }
    }

    public class GenerateSalaryRequest
    {
        public int Year { get; set; }
        public int Month { get; set; }
    }
}
