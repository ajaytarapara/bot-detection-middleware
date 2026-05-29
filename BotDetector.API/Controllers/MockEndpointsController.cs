using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace BotDetector.API.Controllers
{
    [ApiController]
    [Route("api")]
    public class MockEndpointsController : ControllerBase
    {
        [HttpGet("products")]
        public IActionResult GetProducts([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var products = Enumerable.Range((page - 1) * pageSize + 1, pageSize)
                .Select(id => new { Id = id, Name = $"Product {id}", Price = id * 10.99, Stock = id % 5 == 0 ? 0 : 50 })
                .ToList();

            return Ok(new
            {
                Page = page,
                PageSize = pageSize,
                Total = 3000,
                Items = products
            });
        }

        [HttpGet("products/{id:int}")]
        public IActionResult GetProduct(int id)
        {
            if (id <= 0 || id > 3000)
            {
                return NotFound(new { Message = "Product not found" });
            }

            return Ok(new
            {
                Id = id,
                Name = $"Product {id}",
                Description = $"Detailed description for Product {id}",
                Price = id * 10.99,
                Stock = id % 5 == 0 ? 0 : 50,
                SellerId = (id % 300) + 1,
                UpdatedAt = DateTime.UtcNow
            });
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string q)
        {
            // Simulate Elasticsearch load with artificial 100-500ms delay
            var random = new Random();
            int delay = random.Next(100, 501);
            await Task.Delay(delay);

            var term = q ?? string.Empty;
            var results = Enumerable.Range(1, 5)
                .Select(i => new { Id = i, Name = $"Search Result {i} for '{term}'", Score = 1.0 / i })
                .ToList();

            return Ok(new
            {
                Query = term,
                SearchTimeMs = delay,
                Results = results
            });
        }

        [HttpGet("prices/{id:int}")]
        public IActionResult GetPrice(int id)
        {
            if (id <= 0 || id > 3000)
            {
                return NotFound(new { Message = "Product not found" });
            }

            return Ok(new
            {
                ProductId = id,
                Price = id * 10.99,
                OriginalPrice = id * 12.99,
                Currency = "USD",
                DiscountPercentage = 15.3,
                SellerId = (id % 300) + 1
            });
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { Message = "Username and password are required" });
            }

            // Simulate simple credential verification
            if (request.Username == "admin" && request.Password == "password123")
            {
                return Ok(new
                {
                    Token = "mock_jwt_token_for_admin",
                    ExpiresInSeconds = 3600,
                    Username = request.Username
                });
            }

            return Unauthorized(new { Message = "Invalid credentials" });
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
