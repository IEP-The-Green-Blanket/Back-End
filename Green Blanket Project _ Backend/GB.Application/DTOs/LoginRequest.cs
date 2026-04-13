using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations; // Added for validation
using System.Text;

namespace GB.Application.DTOs
{
    /// <summary>
    /// Data Transfer Object for user login requests.
    /// </summary>
    public class LoginRequest
    {
        // Adding [Required] ensures the API returns a 400 Bad Request 
        // immediately if the frontend sends an empty username.
        [Required(ErrorMessage = "Username is required.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; } = string.Empty;
    }
}