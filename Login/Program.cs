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
    public DbSet<Entry> Entries { get; set; }


    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>().HasKey(c => c.Cus_ID); // Primary key
        modelBuilder.Entity<Loan>().HasKey(l => l.Loan_No); // Primary key
        modelBuilder.Entity<Entry>().HasKey(e => e.Entry_ID); // Primary key
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

public class Entry
{
    public int Entry_ID { get; set; }
    public int Loan_No { get; set; }
    public int Cus_ID { get; set; }
    public DateTime Pay_Date { get; set; }
    public long Pay_Amount { get; set; }
    public DateTime Validity { get; set; }
    public string Pay_Type { get; set; } = "Cash";
    public string Entry_Type { get; set; } = "Interest";
}


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

    [HttpGet("customer")] // Updated to avoid route conflict
    public async Task<IActionResult> GetLoansByCustomer([FromQuery] int cusId)
    {
        if (cusId <= 0)
            return BadRequest(new { message = "Invalid customer ID." });

        var loans = await _context.Loans.Where(l => l.Cus_ID == cusId).ToListAsync();
        if (!loans.Any())
            return NotFound(new { message = "No loans found for the given customer." });

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

[ApiController]
[Route("api/entries")]
public class EntriesController : ControllerBase
{
    private readonly AppDbContext _context;

    public EntriesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> CreateEntry([FromBody] Entry entry)
    {
        if (entry == null)
            return BadRequest("Invalid entry data.");

        _context.Entries.Add(entry);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetEntry), new { id = entry.Entry_ID }, entry);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetEntry(int id)
    {
        var entry = await _context.Entries.FindAsync(id);
        if (entry == null)
            return NotFound(new { message = "Entry not found." });

        return Ok(entry);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllEntries()
    {
        var entries = await _context.Entries.ToListAsync();
        return Ok(entries);
    }

    [HttpGet("customer")] // Updated to avoid route conflict
    public async Task<IActionResult> GetEntriesByCustomer([FromQuery] int cusId)
    {
        if (cusId <= 0)
            return BadRequest(new { message = "Invalid customer ID." });

        var entries = await _context.Entries.Where(e => e.Cus_ID == cusId).ToListAsync();
        if (!entries.Any())
            return NotFound(new { message = "No entries found for the given customer." });

        return Ok(entries);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateEntry(int id, [FromBody] Entry entry)
    {
        if (id != entry.Entry_ID)
            return BadRequest("Entry ID mismatch.");

        var existingEntry = await _context.Entries.FindAsync(id);
        if (existingEntry == null)
            return NotFound(new { message = "Entry not found." });

        existingEntry.Loan_No = entry.Loan_No;
        existingEntry.Cus_ID = entry.Cus_ID;
        existingEntry.Pay_Date = entry.Pay_Date;
        existingEntry.Pay_Amount = entry.Pay_Amount;
        existingEntry.Validity = entry.Validity;
        existingEntry.Pay_Type = entry.Pay_Type;
        existingEntry.Entry_Type = entry.Entry_Type;

        _context.Entry(existingEntry).State = EntityState.Modified;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEntry(int id)
    {
        var entry = await _context.Entries.FindAsync(id);
        if (entry == null)
            return NotFound(new { message = "Entry not found." });

        _context.Entries.Remove(entry);
        await _context.SaveChangesAsync();

        return NoContent();
    }
    [HttpGet("expiring-in-one-month")]
    public async Task<IActionResult> GetLoansExpiringInOneMonth()
    {
        try
        {
            // Query to find loans expiring in one month
            var result = await _context.Entries
                .GroupBy(entry => entry.Loan_No)
                .Select(group => new
                {
                    Loan_No = group.Key,
                    MaxValidityDate = group.Max(e => e.Validity)
                })
                .Join(_context.Loans,
                      groupedEntry => groupedEntry.Loan_No,
                      loan => loan.Loan_No,
                      (groupedEntry, loan) => new { groupedEntry, loan })
                .Join(_context.Customers,
                      joined => joined.loan.Cus_ID,
                      customer => customer.Cus_ID,
                      (joined, customer) => new
                      {
                          Loan_No = joined.groupedEntry.Loan_No,
                          MaxValidityDate = joined.groupedEntry.MaxValidityDate,
                          LoanDetails = new
                          {
                              joined.loan.Loan_No,
                              joined.loan.Amount,
                              joined.loan.Cus_ID,
                              joined.loan.Status
                          },
                          CustomerDetails = new
                          {
                              customer.FirstName,
                              customer.LastName,
                              customer.Address
                          }
                      })
                .Where(x => x.LoanDetails.Status == "Open" &&
                            x.MaxValidityDate != null &&
                            EF.Functions.DateDiffMonth(x.MaxValidityDate, DateTime.Now) >= 1)
                .ToListAsync();

            if (result == null || result.Count == 0)
            {
                return NotFound("No loans found with a Validity date difference of more than one month.");
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            // Log the exception details
            Console.WriteLine($"Error: {ex.Message}\n{ex.StackTrace}");
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

}



