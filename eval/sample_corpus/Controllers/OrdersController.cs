using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SampleShop.Data;
using SampleShop.Services;

namespace SampleShop.Controllers;

// REST API endpoints for placing and retrieving customer orders.
[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly EmailService _email;

    public OrdersController(AppDbContext db, EmailService email)
    {
        _db = db;
        _email = email;
    }

    // Creates a new order, persists it, and sends a confirmation email.
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] Order order)
    {
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        await _email.SendOrderConfirmationAsync(order.CustomerUsername, order.Id);
        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }

    // Returns a single order by its identifier.
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOrder(int id)
    {
        var order = await _db.Orders.FindAsync(id);
        return order is null ? NotFound() : Ok(order);
    }
}
