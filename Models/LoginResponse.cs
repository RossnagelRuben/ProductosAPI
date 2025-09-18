namespace BlazorApp_ProductosAPI.Models
{
    public class LoginResponse
    {
        public string Status { get; set; } = string.Empty;
        public LoginData? Data { get; set; }
    }

    public class LoginData
    {
        public string User { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public DateTime DateUTC { get; set; }
    }
}
