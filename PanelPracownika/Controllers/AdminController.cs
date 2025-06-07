using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PanelPracownika.Data;
using PanelPracownika.Models;
using System.Globalization;
using System.Reflection.PortableExecutable;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PanelPracownika.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        private int? GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return int.TryParse(claim?.Value, out int id) ? id : (int?)null;
        }

        private bool IsAdmin()
        {
            var userId = GetUserId();
            if (userId == null) return false;

            return _context.Users
                .Any(u => u.Id == userId.Value && u.IsAdmin == true);
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            if (!IsAdmin()) return Forbid();

            var users = await _context.Users
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Name,
                    u.Surname,
                    u.IsAdmin,
                    Salary = _context.UserSalaries.FirstOrDefault(s => s.UserId == u.Id)
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
        {
            if (!IsAdmin()) return Forbid();

            var user = new Login
            {
                Username = dto.Username,
                Name = dto.Name,
                Surname = dto.Surname,
                Password = new Login().HashPassword(dto.Password),
                IsAdmin = dto.IsAdmin
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(user);
        }

        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            if (!IsAdmin()) return Forbid();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPut("users/{id}/salary")]
        public async Task<IActionResult> UpdateUserSalary(int id, [FromBody] UpdateUserSalaryDto dto)
        {
            if (!IsAdmin()) return Forbid();

            var salary = await _context.UserSalaries.FirstOrDefaultAsync(s => s.UserId == id);
            if (salary == null)
            {
                salary = new UserSalary { UserId = id };
                _context.UserSalaries.Add(salary);
            }

            salary.ContractType = dto.ContractType;
            salary.HourlyRate = dto.HourlyRate;
            salary.MonthlySalary = dto.MonthlySalary;

            await _context.SaveChangesAsync();
            return Ok(salary);
        }

        [HttpPost("users/{userId}/tasks")]
        public async Task<IActionResult> AddTaskToUser(int userId, [FromBody] CreateTaskDto dto)
        {
            if (!IsAdmin()) return Forbid();

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound("Nie znaleziono użytkownika.");

            var task = new UserTask
            {
                UserId = userId,
                Text = dto.Text,
                DueDate = dto.DueDate,
                Completed = false
            };

            _context.UserTasks.Add(task);
            await _context.SaveChangesAsync();

            return Ok(task);
        }

        [HttpDelete("tasks/{taskId}")]
        public async Task<IActionResult> DeleteTask(int taskId)
        {
            if (!IsAdmin()) return Forbid();

            var task = await _context.UserTasks.FindAsync(taskId);
            if (task == null) return NotFound("Nie znaleziono zadania.");

            _context.UserTasks.Remove(task);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("tasks/{userId}")]
        public async Task<IActionResult> GetUserTasks(int userId)
        {
            if (!IsAdmin()) return Forbid();

            var tasks = await _context.UserTasks
                .Where(t => t.UserId == userId)
                .Select(t => new
                {
                    t.Id,
                    t.Text,
                    DueDate = t.DueDate.HasValue
                        ? t.DueDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        : null,
                    t.Completed
                })
                .ToListAsync();

            return Ok(tasks);
        }

        [HttpPut("users/{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserAdminDto dto)
        {
            if (!IsAdmin()) return Forbid();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound("Nie znaleziono użytkownika.");

            if (!string.IsNullOrEmpty(dto.Username))
                user.Username = dto.Username;

            if (!string.IsNullOrEmpty(dto.Name))
                user.Name = dto.Name;

            if (!string.IsNullOrEmpty(dto.Surname))
                user.Surname = dto.Surname;

            if (!string.IsNullOrEmpty(dto.Password))
                user.Password = new Login().HashPassword(dto.Password);

            if (dto.IsAdmin.HasValue)
                user.IsAdmin = dto.IsAdmin.Value;

            await _context.SaveChangesAsync();
            return Ok(user);
        }

        [HttpGet("users/{userId}/worktimes")]
        public async Task<IActionResult> GetUserWorkTimes(int userId, [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
        {
            if (!IsAdmin()) return Forbid();

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound("Nie znaleziono użytkownika.");

            var query = _context.WorkTimes.Where(wt => wt.UserId == userId);

            if (startDate.HasValue)
                query = query.Where(wt => wt.Date >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(wt => wt.Date <= endDate.Value);

            var workTimes = await query
                .OrderByDescending(wt => wt.Date)
                .ThenBy(wt => wt.StartTime)
                .Select(wt => new
                {
                    wt.Id,
                    Date = wt.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    wt.StartTime,
                    wt.EndTime,
                    wt.Total
                })
                .ToListAsync();

            return Ok(workTimes);
        }

    }

    public class CreateUserDto
    {
        public string Username { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
        public string Password { get; set; }
        public bool IsAdmin { get; set; }
    }

    public class UpdateUserSalaryDto
    {
        public string ContractType { get; set; }
        public double? HourlyRate { get; set; }
        public double? MonthlySalary { get; set; }
    }

    public class CreateTaskDto
    {
        public string Text { get; set; }
        public DateTime? DueDate { get; set; }
    }

    public class UpdateUserAdminDto
    {
        public string Username { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
        public string Password { get; set; }
        public bool? IsAdmin { get; set; }
    }
}
