using System;
using System.Collections.Generic;
using System.Text;

using GB.Domain.Enums;

namespace GB.Domain.Entities
{
    // 1. HEADERS: The User Entity
    // We change this to 'public' so the rest of the solution (Application & API) 
    // knows what a UserAccount looks like.
    public class UserAccount
    {
        // 2. HEADERS: Database Identifiers
        // This ID will be the primary key when we connect to PostgreSQL.
        public int Id { get; set; }

        // 3. HEADERS: Authentication Properties
        // We initialize these to empty strings to prevent 'null' errors.
        public string Username { get; set; } = string.Empty;
        
        public string Email {get; set;} = string.Empty;

        // Note: For now, this is a plain text password for testing.
        // Later, we will store a "Hash" here for actual security.
        public string Password { get; set; } = string.Empty;

        // 4. HEADERS: Access Control
        // This links the user to one of the roles we created (Gov, Resident, Tourist).
        public UserRole Role { get; set; }
    }
}