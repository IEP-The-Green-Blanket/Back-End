using System;
using System.Collections.Generic;
using System.Text;
using GB.Domain.Enums;

namespace GB.Domain.Entities
{
    // 1. HEADERS: The User Entity
    // Public visibility allows the Application and Infrastructure layers to map to it.
    public class UserAccount
    {
        // 2. HEADERS: Database Identifiers
        // EF Core automatically recognizes 'Id' as the Primary Key.
        public int Id { get; set; }

        // 3. HEADERS: Authentication Properties
        // Initialized to empty strings to prevent null reference exceptions.
        public string Username { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        // Note: For now, this is a plain text password for testing.
        // Later, we will store a "Hash" here for actual security.
        public string Password { get; set; } = string.Empty;

        // 4. HEADERS: Access Control
        // This links the user to the PostgreSQL 'user_role' column.
        public UserRole Role { get; set; }
    }
}