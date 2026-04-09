using System;
using System.Collections.Generic;
using System.Text;

namespace GB.Application.DTOs
{
    // We make this public so the Web API project can create this object 
    // when a user tries to create a new account.
    public class SignupRequest
    {
        // The username the new user wants to use
        public string Username { get; set; } = string.Empty;

        // The email address for the new account
        public string Email { get; set; } = string.Empty;

        // The password for the new account
        public string Password { get; set; } = string.Empty;

        // The role assigned to the user (e.g., "Tourist", "Resident", "Government")
        public string Role { get; set; } = string.Empty;
    }
}