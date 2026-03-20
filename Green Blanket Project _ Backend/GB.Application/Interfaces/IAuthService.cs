using System;
using System.Collections.Generic;
using System.Text;
using GB.Application.DTOs;

namespace GB.Application.Interfaces
{
    // We change this to a 'public interface'.
    // This is the "contract" for authentication in the Green Blanket project.
    public interface IAuthService
    {
        // Any class that implements this interface MUST provide 
        // a way to verify a user and return a result string.
        string VerifyUser(LoginRequest request);
    }
}
