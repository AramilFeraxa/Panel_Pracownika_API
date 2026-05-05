using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PanelPracownika.Data;
using PanelPracownika.Models;
using PanelPracownika.Services;
using System.Security.Claims;

namespace PanelPracownika.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class WorkTimeController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<WorkTimeController> _logger;

        public WorkTimeController(
            AppDbContext context,
            IEmailService emailService,
            IOptions<EmailSettings> emailSettings,
            ILogger<WorkTimeController> logger)
        {
            _context = context;
            _emailService = emailService;
            _emailSettings = emailSettings.Value;
            _logger = logger;
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
                UserId = userId,
                IsRemote = dto.IsRemote
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
            workTime.IsRemote = dto.IsRemote;

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

        [HttpPost("send-hours-email")]
        public async Task<IActionResult> SendHoursEmail(
            [FromForm] IFormFile file,
            [FromForm] string subject,
            [FromForm] string body
        )
        {
            var recipient = _emailSettings.RecipientEmail;

            _logger.LogInformation(
                "SendHoursEmail started. FilePresent={FilePresent}, FileName={FileName}, FileLength={FileLength}, SubjectLength={SubjectLength}, RecipientConfigured={RecipientConfigured}, FromConfigured={FromConfigured}",
                file != null,
                file?.FileName,
                file?.Length,
                subject?.Length ?? 0,
                !string.IsNullOrWhiteSpace(recipient),
                !string.IsNullOrWhiteSpace(_emailSettings.FromEmail)
            );

            if (string.IsNullOrWhiteSpace(recipient))
            {
                _logger.LogWarning("Email recipient is not configured in EmailSettings.RecipientEmail.");
                return StatusCode(500, "Brak skonfigurowanego adresu odbiorcy w secrets.json.");
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest("Nie przesłano pliku.");
            }

            if (string.IsNullOrWhiteSpace(subject))
            {
                return BadRequest("Nie podano tematu wiadomości.");
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                body = "W załączniku przesyłam zestawienie godzin pracy.";
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized("Invalid or missing user ID in token.");

            var userEmail = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            var fromEmail = string.IsNullOrWhiteSpace(_emailSettings.FromEmail)
                ? userEmail
                : _emailSettings.FromEmail;

            var replyToEmail = string.IsNullOrWhiteSpace(userEmail)
                ? fromEmail
                : userEmail;

            _logger.LogInformation(
                "Resolved email addresses. UserEmailPresent={UserEmailPresent}, FromEmailResolved={FromEmailResolved}, ReplyToResolved={ReplyToResolved}, Recipient={Recipient}",
                !string.IsNullOrWhiteSpace(userEmail),
                fromEmail,
                replyToEmail,
                recipient
            );

            if (string.IsNullOrWhiteSpace(fromEmail))
            {
                _logger.LogWarning("Email from address is not configured and user email is empty.");
                return StatusCode(500, "Brak skonfigurowanego adresu nadawcy w secrets.json.");
            }

            try
            {
                _logger.LogInformation("Sending hours email.");
                await _emailService.SendEmailWithAttachmentAsync(
                    recipient,
                    replyToEmail,
                    subject,
                    body,
                    file
                );

                _logger.LogInformation("Hours email sent successfully.");
                return Ok("Mail został wysłany.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send hours email.");
                return StatusCode(500, $"Błąd podczas wysyłki maila: {ex.Message}");
            }
        }
    }
}