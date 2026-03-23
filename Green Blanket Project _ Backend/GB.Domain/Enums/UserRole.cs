using System;
using System.Collections.Generic;
using System.Text;

namespace GB.Domain.Enums
{
    // Change 'internal class' to 'public enum'
    // This allows the API and Application projects to access these roles.
    public enum UserRole
    {
        Government,
        Tourist,
        Resident
    }
}