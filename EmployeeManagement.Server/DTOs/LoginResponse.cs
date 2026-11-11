namespace EmployeeManagement.Server.DTOs
{
    // Pergjigjja e login-it te suksesshem: token-i perdoret ne header Authorization: Bearer ...
    // Nuk eshte sesion i ruajtur ne kete DTO; eshte informacioni qe klienti merr nga API.
    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;

        // Klienti mund te perdore kete kohe UTC per te kuptuar kur token-i nuk vlen me.
        public DateTime ExpiresAt { get; set; }

        public LoginUserResponse User { get; set; } = new();
    }

    // Permbledhja e profilit ne pergjigjen e login-it; nuk permban password ose hash.
    // Roles ndihmon nderfaqen te shfaqe veprimet e duhura, por API kontrollon vete lejet me Authorize.
    public class LoginUserResponse
    {
        public string Id { get; set; } = string.Empty;

        public string? Email { get; set; }

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public IList<string> Roles { get; set; } = new List<string>();
    }
}
