using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PanelPracownika.Data;
using System.Text;
using PanelPracownika.Models;
using PanelPracownika.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "http://localhost:3000",
            ValidAudience = "http://localhost:3000",
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:SecretKey"])
            )
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddDbContext<AppDbContext>(options =>
       options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"),
              new MariaDbServerVersion(new Version(10, 6, 18))));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontendOrigins", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "https://host656095.xce.pl",
                "https://panel.xce.pl",
                "http://192.168.1.58:3000"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddHttpClient(nameof(EmailService));

builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddControllers();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var connection = db.Database.GetDbConnection();
    connection.Open();

    bool ColumnExists(string tableName, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName AND COLUMN_NAME = @columnName";

        var tableParameter = command.CreateParameter();
        tableParameter.ParameterName = "@tableName";
        tableParameter.Value = tableName;
        command.Parameters.Add(tableParameter);

        var columnParameter = command.CreateParameter();
        columnParameter.ParameterName = "@columnName";
        columnParameter.Value = columnName;
        command.Parameters.Add(columnParameter);

        var result = command.ExecuteScalar();
        return Convert.ToInt32(result) > 0;
    }

    if (!ColumnExists("Users", "Email"))
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE `Users` ADD COLUMN `Email` longtext NOT NULL DEFAULT ''");
    }

    if (!ColumnExists("AbsenceDates", "Reason"))
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE `AbsenceDates` ADD COLUMN `Reason` longtext NOT NULL DEFAULT ''");
    }

    var absences = await db.AbsenceDates.ToListAsync();
    foreach (var absence in absences)
    {
        if (string.Equals(absence.Type, "Urlop", StringComparison.OrdinalIgnoreCase) || string.Equals(absence.Type, "Delegacja", StringComparison.OrdinalIgnoreCase))
        {
            absence.Reason = string.IsNullOrWhiteSpace(absence.Reason) ? absence.Type : absence.Reason;
            absence.Type = "Wyjazd";
        }

        if (string.IsNullOrWhiteSpace(absence.Type))
            absence.Type = "Wyjazd";

        if (string.IsNullOrWhiteSpace(absence.Reason))
            absence.Reason = "Urlop";
    }

    var users = await db.Users.ToListAsync();
    foreach (var user in users)
    {
        user.Email ??= string.Empty;
    }

    db.SaveChanges();
}

//app.UseHttpsRedirection();
app.UseCors("AllowFrontendOrigins");
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
