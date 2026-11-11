using EmployeeManagement.Server.DTOs;

namespace EmployeeManagement.Server.Interfaces
{
    // AuthController mbeshtetet ne kete kontrate; AuthService mban zbatimin e Identity dhe JWT.
    // Interface nuk krijon llogari dhe nuk percakton endpoint publik regjistrimi.
    public interface IAuthService
    {
        // Merr kredencialet; kthen pergjigjen me token ose null kur email/password nuk verifikohen.
        Task<LoginResponse?> LoginAsync(LoginRequest request);
    }
}
