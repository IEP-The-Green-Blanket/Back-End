using System;
using System.Threading.Tasks; // Required for Task
using System.Linq;
using Microsoft.EntityFrameworkCore; // Required for FirstOrDefaultAsync & AnyAsync
using GB.Application.DTOs;
using GB.Application.Interfaces;
using GB.Domain.Entities;
using GB.Domain.Enums;
using GB.Infrastructure;

namespace GB.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly GreenBlanketDbContext _context;

        // 1. HEADERS: Database Injection
        // Removed IHostEnvironment and the mock list. We trust the SSH tunnel to route us
        // to PostgreSQL during Development.
        public AuthService(GreenBlanketDbContext context)
        {
            _context = context;
        }

        // 2. HEADERS: Verification Logic (Login)
        // Changed to async Task to prevent blocking the API thread while waiting on the tunnel.
        public async Task<string> VerifyUser(LoginRequest request)
        {
            // Direct query to the database using Entity Framework Core's async methods
            var dbUser = await _context.UserAccounts.FirstOrDefaultAsync(u =>
                u.Username.ToLower() == request.Username.ToLower());

            if (dbUser == null)
            {
                return "Verification Failed: User does not exist.";
            }

            if (dbUser.Password != request.Password)
            {
                return "Verification Failed: Incorrect password.";
            }

            return $"Access Granted. Welcome back! Your assigned role is: {dbUser.Role}";
        }

        // 3. HEADERS: Registration Logic (Signup)
        // Added this method to fulfill the updated IAuthService contract.
        public async Task<string> RegisterUser(SignupRequest request)
        {
            // Prevent database crashes by checking if the username/email already exists
            var userExists = await _context.UserAccounts.AnyAsync(u =>
                u.Username.ToLower() == request.Username.ToLower() ||
                u.Email.ToLower() == request.Email.ToLower());

            if (userExists)
            {
                return "Registration Failed: Username or Email is already in use.";
            }

            // Convert the string role from the frontend into your C# UserRole Enum.
            // If they send junk data, default them to 'Tourist'.
            if (!Enum.TryParse<UserRole>(request.Role, true, out var assignedRole))
            {
                assignedRole = UserRole.Tourist;
            }

            var newUser = new UserAccount
            {
                Username = request.Username,
                Email = request.Email,
                Password = request.Password,
                Role = assignedRole
            };

            // Add the new user and save the changes asynchronously
            _context.UserAccounts.Add(newUser);
            await _context.SaveChangesAsync();

            return "Success: Account created successfully.";
        }
    }
}