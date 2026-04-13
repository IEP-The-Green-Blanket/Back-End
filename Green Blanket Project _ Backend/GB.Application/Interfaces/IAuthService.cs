using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks; // Required for async database operations
using GB.Application.DTOs;

namespace GB.Application.Interfaces
{
    /// <summary>
    /// The 'contract' for authentication logic. 
    /// Updated to handle asynchronous database calls for the Harties DB.
    /// </summary>
    public interface IAuthService
    {
        // 1. LOGIN CONTRACT:
        // Changed to 'Task<string>' so the engine can 'await' the PostgreSQL response.
        Task<string> VerifyUser(LoginRequest request);

        // 2. SIGNUP CONTRACT:
        // This was missing! It allows the Controller to talk to the Registration logic.
        Task<string> RegisterUser(SignupRequest request);
    }
}