using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System.Security.Cryptography;
using System.Text;

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
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Loan API", Version = "v1" });
});

var app = builder.Build();

// Enable Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Loan API v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseCors(); // Enable CORS globally
app.UseAuthorization();
app.MapControllers();
app.Run();

#region Database and Entities
public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Loan> Loans { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>().HasKey(c => c.Cus_ID); // Primary key
        modelBuilder.Entity<Loan>().HasKey(l => l.Loan_No); // Primary key
    }
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
}

public class Customer
{
    public int Cus_ID { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FatherName { get; set; } = string.Empty;
    public string MotherName { get; set; } = string.Empty;
    public string MobileNo { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class Loan
{
    public int Loan_No { get; set; }
    public int Cus_ID { get; set; }
    public string LoanType { get; set; } = string.Empty;

    // Use double instead of float
    public double Amount { get; set; }
    public double Interest { get; set; }

    public DateTime DOB { get; set; }
    public string Document { get; set; } = string.Empty;
    public string AdvancePay { get; set; } = string.Empty;
    public string Status { get; set; } = "Normal";
}

#endregion

#region Controllers
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;

    public AuthController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] User user)
    {
        if (string.IsNullOrWhiteSpace(user.Username) || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return BadRequest("Username and password are required.");
        }

        if (_context.Users.Any(u => u.Username == user.Username))
        {
            return BadRequest("User already exists.");
        }

        using var sha256 = SHA256.Create();
        user.PasswordHash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(user.PasswordHash)));

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok("User registered successfully.");
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] User login)
    {
        if (string.IsNullOrWhiteSpace(login.Username) || string.IsNullOrWhiteSpace(login.PasswordHash))
        {
            return BadRequest("Username and password are required.");
        }

        using var sha256 = SHA256.Create();
        var hash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(login.PasswordHash)));

        var user = _context.Users.FirstOrDefault(u => u.Username == login.Username && u.PasswordHash == hash);
        if (user == null)
        {
            return Unauthorized("Invalid credentials.");
        }

        return Ok(new { message = "Login successful", username = user.Username });
    }
}

[ApiController]
[Route("api/customers")]
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _context;

    public CustomersController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> CreateCustomer([FromBody] Customer customer)
    {
        if (customer == null)
            return BadRequest("Invalid customer data.");

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetCustomer), new { id = customer.Cus_ID }, customer);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCustomer(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
            return NotFound(new { message = "Customer not found." });

        return Ok(customer);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllCustomers()
    {
        var customers = await _context.Customers.ToListAsync();
        return Ok(customers);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCustomer(int id, [FromBody] Customer customer)
    {
        if (id != customer.Cus_ID)
            return BadRequest("Customer ID mismatch.");

        var existingCustomer = await _context.Customers.FindAsync(id);
        if (existingCustomer == null)
            return NotFound(new { message = "Customer not found." });

        existingCustomer.FirstName = customer.FirstName;
        existingCustomer.LastName = customer.LastName;
        existingCustomer.FatherName = customer.FatherName;
        existingCustomer.MotherName = customer.MotherName;
        existingCustomer.MobileNo = customer.MobileNo;
        existingCustomer.Address = customer.Address;
        existingCustomer.Type = customer.Type;

        _context.Entry(existingCustomer).State = EntityState.Modified;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCustomer(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
            return NotFound(new { message = "Customer not found." });

        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

[ApiController]
[Route("api/loans")]
public class LoansController : ControllerBase
{
    private readonly AppDbContext _context;

    public LoansController(AppDbContext context)
    {
        _context = context;
    }


    [HttpPost]
    public async Task<IActionResult> CreateLoan([FromBody] Loan loan)
    {
        if (loan == null)
            return BadRequest("Invalid loan data.");

        _context.Loans.Add(loan);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetLoan), new { id = loan.Loan_No }, loan);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetLoan(int id)
    {
        var loan = await _context.Loans.FindAsync(id);
        if (loan == null)
            return NotFound(new { message = "Loan not found." });

        return Ok(loan);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllLoans()
    {
        var loans = await _context.Loans.ToListAsync();
        return Ok(loans);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateLoan(int id, [FromBody] Loan loan)
    {
        if (id != loan.Loan_No)
            return BadRequest("Loan ID mismatch.");

        var existingLoan = await _context.Loans.FindAsync(id);
        if (existingLoan == null)
            return NotFound(new { message = "Loan not found." });

        existingLoan.Cus_ID = loan.Cus_ID;
        existingLoan.LoanType = loan.LoanType;
        existingLoan.Amount = loan.Amount;
        existingLoan.Interest = loan.Interest;
        existingLoan.DOB = loan.DOB;
        existingLoan.Document = loan.Document;
        existingLoan.AdvancePay = loan.AdvancePay;
        existingLoan.Status = loan.Status;

        _context.Entry(existingLoan).State = EntityState.Modified;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteLoan(int id)
    {
        var loan = await _context.Loans.FindAsync(id);
        if (loan == null)
            return NotFound(new { message = "Loan not found." });

        _context.Loans.Remove(loan);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
#endregion
