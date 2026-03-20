using GB.Application.DTOs;
using GB.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Green_Blanket_Project___Backend.Controllers
{
    // 1. HEADERS: API Route Definition
    // This tells the app that this controller is reachable at 'api/auth'
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        // 2. HEADERS: Dependency Injection
        // This 'injects' the login logic we wrote in the Application layer
        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        // 3. HEADERS: The Login Endpoint
        // This is the specific URL (POST api/auth/login) the frontend will call
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            // Call the verification logic from our AuthService
            var result = _authService.VerifyUser(request);

            // If the message contains "Failed", return a 401 Unauthorized status
            if (result.Contains("Failed"))
            {
                return Unauthorized(new { message = result });
            }

            // Otherwise, return a 200 OK status with the success message and role
            return Ok(new { message = result });
        }
    }
}