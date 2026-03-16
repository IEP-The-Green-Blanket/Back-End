using System;
using System.Collections.Generic;
using System.Text;
using GB.Application.DTOs;
using GB.Application.Interfaces;
using GB.Domain.Entities;
using GB.Domain.Enums;
using System.Collections.Generic;
using System.Linq;

namespace GB.Application.Services
{
    // We change this to 'public' and add ': IAuthService' to implement the interface.
    public class AuthService : IAuthService
    {
        // 1. HEADERS: Mock Database
        // This is a temporary list of users for the Hartbeespoort project.
        // Once the Infrastructure project is ready, we will replace this with PostgreSQL.
        private readonly List<UserAccount> _mockUserDb = new List<UserAccount>
        {
            new UserAccount { Username = "harties_gov", Password = "GovPassword123", Role = UserRole.Government },
            new UserAccount { Username = "dam_resident", Password = "ResidentPass", Role = UserRole.Resident },
            new UserAccount { Username = "tourist_ssa", Password = "Holiday2026", Role = UserRole.Tourist }
        };

        // 2. HEADERS: Verification Logic
        public string VerifyUser(LoginRequest request)
        {
            // First, find the user in our mock list by username (ignoring capital letters)
            var user = _mockUserDb.FirstOrDefault(u =>
                u.Username.Equals(request.Username, System.StringComparison.OrdinalIgnoreCase));

            // If the user doesn't exist in our records
            if (user == null)
            {
                return "Verification Failed: User does not exist.";
            }

            // Check if the provided password matches the one in our "database"
            if (user.Password != request.Password)
            {
                return "Verification Failed: Incorrect password.";
            }

            // 3. HEADERS: Role Assignment
            // If we get here, the login is successful. We return the role.
            return $"Access Granted. Welcome back! Your assigned role is: {user.Role}";
        }
    }
}