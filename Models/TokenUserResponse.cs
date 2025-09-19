namespace BlazorApp_ProductosAPI.Models
{
    public class TokenUserResponse
    {
        public string? Status { get; set; }
        public TokenUserData? Data { get; set; }
        public string? Message { get; set; }
    }

    public class TokenUserData
    {
        public int EmpresaID { get; set; }
        public int EComLoginID { get; set; }
        public string? Token { get; set; }
        public string? UserName { get; set; }
        public object? NivelAcceso { get; set; }
        public object? TipoAcceso { get; set; }
        public object? NroEntidad { get; set; }
        public object? TipoAccesoID { get; set; }
        public List<object>? Permisos { get; set; }
        public EntidadSucursal? EntidadSucursal { get; set; }
        public DateTime? FechaVencimiento { get; set; }
        public List<Deposito>? Depositos { get; set; }
        public UsuarioAlmaNET? UsuarioAlmaNET { get; set; }
    }

    public class EntidadSucursal
    {
        public string? RazonSocial { get; set; }
        public int EntidadSucID { get; set; }
        public long NroIdentificacion { get; set; }
        public Empleado? Empleado { get; set; }
        public object? Cliente { get; set; }
        public object? Proveedor { get; set; }
        public Vendedor? Vendedor { get; set; }
        public Usuario? Usuario { get; set; }
    }

    public class Empleado
    {
        public int EmpleadoID { get; set; }
        public bool Inhabilitado { get; set; }
        public object? Conductor { get; set; }
        public EncargadoDeposito? EncargadoDeposito { get; set; }
    }

    public class EncargadoDeposito
    {
        public int EncargDepositoID { get; set; }
        public List<Deposito>? Depositos { get; set; }
    }

    public class Deposito
    {
        public int DepositoID { get; set; }
        public string? Descripcion { get; set; }
    }

    public class Vendedor
    {
        public int VendedorID { get; set; }
        public bool Inhabilitado { get; set; }
        public object? Config { get; set; }
    }

    public class Usuario
    {
        public int AlmaUserID { get; set; }
        public string? UserName { get; set; }
        public object? UsuarioPerfilID { get; set; }
        public int PerfilID { get; set; }
        public int SucursalID { get; set; }
        public object? ExeptoSucursal { get; set; }
        public int SectorID { get; set; }
        public object? ExeptoSector { get; set; }
        public object? RestringirAccion { get; set; }
    }

    public class UsuarioAlmaNET
    {
        public int AlmaUserID { get; set; }
        public string? UserName { get; set; }
        public object? UsuarioPerfilID { get; set; }
        public int PerfilID { get; set; }
        public int SucursalID { get; set; }
        public object? ExeptoSucursal { get; set; }
        public int SectorID { get; set; }
        public object? ExeptoSector { get; set; }
        public object? RestringirAccion { get; set; }
    }
}
