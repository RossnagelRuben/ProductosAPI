namespace BlazorApp_ProductosAPI.Models
{
    public class LoginRequest
    {
        public string User { get; set; } = string.Empty;
        public string Pwd { get; set; } = string.Empty;
        public bool UsePublicLogin { get; set; } = false;
    }
}
