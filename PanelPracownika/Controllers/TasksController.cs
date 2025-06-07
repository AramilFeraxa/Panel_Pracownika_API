using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using PanelPracownika.Models;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PanelPracownika.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TasksController : ControllerBase
    {
        private readonly string _connectionString;

        public TasksController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
        }

        private bool IsAdmin()
        {
            var claim = User.FindFirst("IsAdmin");
            return claim != null && claim.Value == "1";
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserTask>>> GetTasks()
        {
            var tasks = new List<UserTask>();
            int userId = GetUserId();
            bool isAdmin = IsAdmin();

            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = isAdmin
                    ? "SELECT * FROM UserTasks"
                    : "SELECT * FROM UserTasks WHERE UserId = @UserId";

                using (var command = new MySqlCommand(query, connection))
                {
                    if (!isAdmin)
                        command.Parameters.AddWithValue("@UserId", userId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            tasks.Add(new UserTask
                            {
                                Id = reader.GetInt32("Id"),
                                Text = reader.GetString("Text"),
                                DueDate = reader.IsDBNull("DueDate") ? (DateTime?)null : reader.GetDateTime("DueDate"),
                                Completed = reader.GetBoolean("Completed"),
                                UserId = reader.GetInt32("UserId")
                            });
                        }
                    }
                }
            }

            return tasks;
        }

        [HttpPost]
        public async Task<ActionResult<UserTask>> PostTask(UserTask task)
        {
            int userId = GetUserId();
            bool isAdmin = IsAdmin();

            if (!isAdmin || task.UserId == 0)
                task.UserId = userId;

            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = "INSERT INTO UserTasks (Text, DueDate, Completed, UserId) VALUES (@Text, @DueDate, @Completed, @UserId)";

                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Text", task.Text);
                    command.Parameters.AddWithValue("@DueDate", task.DueDate ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Completed", task.Completed);
                    command.Parameters.AddWithValue("@UserId", task.UserId);

                    await command.ExecuteNonQueryAsync();
                    task.Id = (int)command.LastInsertedId;
                }
            }

            return CreatedAtAction(nameof(GetTask), new { id = task.Id }, task);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserTask>> GetTask(int id)
        {
            UserTask task = null;
            int userId = GetUserId();
            bool isAdmin = IsAdmin();

            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = isAdmin
                    ? "SELECT * FROM UserTasks WHERE Id = @Id"
                    : "SELECT * FROM UserTasks WHERE Id = @Id AND UserId = @UserId";

                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Id", id);
                    if (!isAdmin)
                        command.Parameters.AddWithValue("@UserId", userId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            task = new UserTask
                            {
                                Id = reader.GetInt32("Id"),
                                Text = reader.GetString("Text"),
                                DueDate = reader.IsDBNull("DueDate") ? (DateTime?)null : reader.GetDateTime("DueDate"),
                                Completed = reader.GetBoolean("Completed"),
                                UserId = reader.GetInt32("UserId")
                            };
                        }
                    }
                }
            }

            if (task == null) return NotFound();
            return task;
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutTask(int id, UserTask task)
        {
            if (id != task.Id) return BadRequest();

            int userId = GetUserId();
            bool isAdmin = IsAdmin();

            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = "UPDATE UserTasks SET Text = @Text, DueDate = @DueDate, Completed = @Completed WHERE Id = @Id" +
                               (isAdmin ? "" : " AND UserId = @UserId");

                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Id", task.Id);
                    command.Parameters.AddWithValue("@Text", task.Text);
                    command.Parameters.AddWithValue("@DueDate", task.DueDate ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Completed", task.Completed);
                    if (!isAdmin)
                        command.Parameters.AddWithValue("@UserId", userId);

                    int result = await command.ExecuteNonQueryAsync();
                    if (result == 0) return NotFound();
                }
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTask(int id)
        {
            int userId = GetUserId();
            bool isAdmin = IsAdmin();

            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = "DELETE FROM UserTasks WHERE Id = @Id" +
                               (isAdmin ? "" : " AND UserId = @UserId");

                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Id", id);
                    if (!isAdmin)
                        command.Parameters.AddWithValue("@UserId", userId);

                    int result = await command.ExecuteNonQueryAsync();
                    if (result == 0) return NotFound();
                }
            }

            return NoContent();
        }
    }
}
