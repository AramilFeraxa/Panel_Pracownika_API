using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using PanelPracownika.Models;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;

namespace PanelPracownika.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TasksController : ControllerBase
    {
        private readonly string _connectionString;

        public TasksController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserTask>>> GetTasks()
        {
            var tasks = new List<UserTask>();

            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = "SELECT * FROM UserTasks";

                using (var command = new MySqlCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        tasks.Add(new UserTask
                        {
                            Id = reader.GetInt32("Id"),
                            Text = reader.GetString("Text"),
                            DueDate = reader.IsDBNull("DueDate")
                        ? null
                        : reader.GetDateTime("DueDate").ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                            Completed = reader.GetBoolean("Completed")
                        });
                    }
                }
            }

            return tasks;
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserTask>> GetTask(int id)
        {
            UserTask task = null;

            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = "SELECT * FROM UserTasks WHERE Id = @Id";

                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Id", id);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            task = new UserTask
                            {
                                Id = reader.GetInt32("Id"),
                                Text = reader.GetString("Text"),
                                DueDate = reader.IsDBNull("DueDate")
                        ? null
                        : reader.GetDateTime("DueDate").ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                                Completed = reader.GetBoolean("Completed")
                            };
                        }
                    }
                }
            }

            if (task == null)
            {
                return NotFound();
            }

            return task;
        }

        [HttpPost]
        public async Task<ActionResult<UserTask>> PostTask(UserTask task)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = "INSERT INTO UserTasks (Text, DueDate, Completed) VALUES (@Text, @DueDate, @Completed)";

                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Text", task.Text);
                    command.Parameters.AddWithValue("@DueDate", task.DueDate ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Completed", task.Completed);

                    await command.ExecuteNonQueryAsync();
                    task.Id = (int)command.LastInsertedId;
                }
            }

            return CreatedAtAction(nameof(GetTask), new { id = task.Id }, task);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutTask(int id, UserTask task)
        {
            if (id != task.Id)
            {
                return BadRequest();
            }

            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = "UPDATE UserTasks SET Text = @Text, DueDate = @DueDate, Completed = @Completed WHERE Id = @Id";

                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Id", task.Id);
                    command.Parameters.AddWithValue("@Text", task.Text);
                    command.Parameters.AddWithValue("@DueDate", task.DueDate ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Completed", task.Completed);

                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        return NotFound();
                    }
                }
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTask(int id)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = "DELETE FROM UserTasks WHERE Id = @Id";

                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Id", id);
                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        return NotFound();
                    }
                }
            }

            return NoContent();
        }
    }
}
