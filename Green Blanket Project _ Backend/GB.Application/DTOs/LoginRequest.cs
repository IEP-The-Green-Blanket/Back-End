using System;
using System.Collections.Generic;
using System.Text;

namespace GB.Application.DTOs
{
    // We make this public so the Web API project can create this object 
    // when a user tries to log in.
    public class LoginRequest
    {
        // The username entered on the login screen
        public string Username { get; set; } = string.Empty;

        // The password entered on the login screen
        public string Password { get; set; } = string.Empty;
    }
}
