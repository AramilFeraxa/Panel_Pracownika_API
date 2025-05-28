using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PanelPracownika.Data;
using PanelPracownika.Models;

namespace PanelPracownika.Controllers
{
    [Route("api/[controller]")]
    [ApiController]

    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UserController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("login")]
        public async Task<ActionResult> Login([FromBody] LoginRequest request)
        {
            var userFromDB = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);

            if (userFromDB == null || !userFromDB.VerifyPassword(request.Password))
            {
                return Unauthorized("Invalid username or password.");
            }

            var token = userFromDB.GenerateToken(userFromDB.Id.ToString());

            return Ok(new
            {
                Token = token,
                User = new
                {
                    userFromDB.Username,
                    userFromDB.Id
                }
            });
        }


        [HttpGet]
        public async Task<ActionResult<IEnumerable<Login>>> GetUsers()
        {
            var users = await _context.Users.ToListAsync();

            if (users == null)
            {
                return NotFound("No users found.");
            }

            return Ok(users);
        }

        [HttpPost]
        public async Task<ActionResult<Login>> PostUser(Login user)
        {
            user.Password = user.HashPassword(user.Password);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return Ok(user);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, Login user)
        {
            if (id != user.Id)
            {
                return BadRequest("User ID mismatch.");
            }

            var existingUser = await _context.Users.FindAsync(id);

            if (existingUser == null)
            {
                return NotFound("User not found.");
            }

            existingUser.Name = user.Name;
            existingUser.Surname = user.Surname;
            existingUser.Password = user.HashPassword(user.Password);

            await _context.SaveChangesAsync();
            return Ok(existingUser);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound("User not found.");
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return Ok(user);
        }
    }       
}
