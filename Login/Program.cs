using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer("Server=localhost\\SQLEXPRESS;Database=LoginApp;Trusted_Connection=True;"));
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173") // React frontend URL
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Login API", Version = "v1" });
});

var app = builder.Build();

// Enable Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Login API v1");
    c.RoutePrefix = "swagger"; // Ensure Swagger UI is served at /swagger
});

app.UseHttpsRedirection();
app.UseCors();  // Enable CORS globally
app.UseAuthorization();
app.MapControllers();
app.Run();

public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
}

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;

    public AuthController(AppDbContext context)
    {
        _context = context;
    }

    // Register User
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] User user)
    {
        if (string.IsNullOrWhiteSpace(user.Username) || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return BadRequest("Username and password are required.");
        }

        // Check if the user already exists in the database
        if (_context.Users.Any(u => u.Username == user.Username))
        {
            return BadRequest("User already exists.");
        }

        // Password Hashing using SHA256
        using var sha256 = SHA256.Create();
        user.PasswordHash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(user.PasswordHash)));

        // Add the user to the database
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok("User registered successfully.");
    }

    // Login User
    [HttpPost("login")]
    public IActionResult Login([FromBody] User login)
    {
        if (string.IsNullOrWhiteSpace(login.Username) || string.IsNullOrWhiteSpace(login.PasswordHash))
        {
            return BadRequest("Username and password are required.");
        }

        // Hash the password provided by the user
        using var sha256 = SHA256.Create();
        var hash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(login.PasswordHash)));

        // Look for the user in the database with the hashed password
        var user = _context.Users.FirstOrDefault(u => u.Username == login.Username && u.PasswordHash == hash);
        if (user == null)
        {
            return Unauthorized("Invalid credentials.");
        }

        return Ok(new { message = "Login successful", username = user.Username });
    }
}
