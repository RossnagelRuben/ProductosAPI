using BlazorApp_ProductosAPI.Models;
using Microsoft.JSInterop;
using System.Text;
using System.Text.Json;

namespace BlazorApp_ProductosAPI.Services
{
    /// <summary>Resultado de GetTokenUser: éxito, token user, token final (tokenDev.tokenUser) y compañía.</summary>
    public class TokenUserResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Company { get; set; }
        /// <summary>Token de usuario devuelto por GetTokenUser.</summary>
        public string? TokenUser { get; set; }
        /// <summary>Token final para Bearer: tokenDev + "." + tokenUser.</summary>
        public string? TokenFinal { get; set; }
    }
    public interface IAuthService
    {
        Task<LoginResponse?> LoginAsync(string user, string password, string? tokenDevOverride = null, string? tokenEmpresaOverride = null);
        Task<bool> IsLoggedInAsync();
        Task LogoutAsync();
        Task<string?> GetTokenAsync();
        Task<string?> GetUserAsync();
        Task<string> GetLastRawResponseAsync();
        Task<string> GetLastRequestInfoAsync();
        Task<string?> GetTokenDEVAsync();
        Task<string?> GetTokenUSERAsync();
        Task<string?> GetTokenFINALAsync();
        Task<string?> GetCompanyAsync();
    }

    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly IJSRuntime _jsRuntime;
        private const string TOKEN_USER_API_URL = "https://drrsystemas4.azurewebsites.net/Auth/GetTokenUser";
        private const string TOKEN_DEV_API_URL = "https://drrsystemas4.azurewebsites.net/Auth/GetTokenDeveloper";
        private const string TOKEN_DRR = "4a7183cf-9515-4d87-a9f1-a9e1f952cc7c";
        //private const string TOKEN_DEV_FIXED = "E1F018DA-3ECD-424D-B13E-AB3BD6950C83"; //Empresa DEMINISONES
        private const string TOKEN_DEV_FIXED = "4FE21E79-FFF1-4B50-941E-BD3CE2DF84C9"; //Empresa CURSOS
        
        private string _lastRawResponse = string.Empty;
        private string _lastRequestInfo = string.Empty;

        // DTO interno mínimo para la respuesta de GetTokenDeveloper
        private class TokenDeveloperResponse
        {
            public string? Status { get; set; }
            public TokenDeveloperData? Data { get; set; }
            public string? Message { get; set; }
        }

        private class TokenDeveloperData
        {
            public string? Token { get; set; }
        }

        public AuthService(HttpClient httpClient, IJSRuntime jsRuntime)
        {
            _httpClient = httpClient;
            _jsRuntime = jsRuntime;
        }

        /// <summary>
        /// Flujo de login:
        /// 1) Llamar a GetTokenDeveloper (Auth/GetTokenDeveloper) con token de empresa → devuelve TOKEN DEV.
        /// 2) Llamar a GetTokenUser (Auth/GetTokenUser) con TOKEN DEV en Bearer → devuelve TOKEN USER.
        /// 3) Token final para las APIs = TOKEN_DEV + "." + TOKEN_USER (ese es el Bearer que se guarda en auth_token).
        /// </summary>
        public async Task<LoginResponse?> LoginAsync(string user, string password, string? tokenDevOverride = null, string? tokenEmpresaOverride = null)
        {
            try
            {
                // --- Paso 1: Obtener TOKEN DEV ---
                // Si se pasa tokenDevOverride (ej. desde formulario de login en modo debug), se usa ese.
                // Si no, se llama a la API GetTokenDeveloper con el token de empresa (TOKEN_DRR por defecto).
                string? tokenDev;

                if (!string.IsNullOrWhiteSpace(tokenDevOverride))
                {
                    tokenDev = tokenDevOverride.Trim();
                    Console.WriteLine($"Usando TOKEN DEV proporcionado desde el login: {tokenDev}");
                }
                else
                {
                    // TOKEN de empresa: si no se indica en el login, se usa el TOKEN_DRR por defecto
                    var empresaToken = string.IsNullOrWhiteSpace(tokenEmpresaOverride)
                        ? TOKEN_DRR
                        : tokenEmpresaOverride.Trim();

                    var tokenDevResult = await GetTokenDeveloperAsync(user, password, empresaToken);
                    if (!tokenDevResult.Success || string.IsNullOrWhiteSpace(tokenDevResult.TokenDev))
                    {
                        Console.WriteLine($"Error obteniendo TOKEN DEV: {tokenDevResult.ErrorMessage}");
                        return new LoginResponse
                        {
                            Status = "error",
                            Data = null
                        };
                    }

                    tokenDev = tokenDevResult.TokenDev;
                    Console.WriteLine($"TOKEN DEV obtenido correctamente desde API: {tokenDev}");
                }

                // --- Paso 2: Con el TOKEN DEV, obtener TOKEN USER (Auth/GetTokenUser con Bearer = tokenDev) ---
                var tokenUserResult = await GetTokenUserAsync(user, password, tokenDev);
                
                if (tokenUserResult.Success && !string.IsNullOrEmpty(tokenUserResult.TokenFinal))
                {
                    // Token final = tokenDev + "." + tokenUser (es el que debe usarse como Bearer en el resto de APIs)
                    var tokenFinal = tokenUserResult.TokenFinal;

                    // Respuesta de login exitoso; Token en Data es el token final (Bearer) para las llamadas API
                    var loginResponse = new LoginResponse
                    {
                        Status = "ok",
                        Data = new LoginData
                        {
                            User = user,
                            Company = tokenUserResult.Company ?? "DRR Systemas",
                            Token = tokenFinal,
                            DateUTC = DateTime.UtcNow
                        }
                    };

                    // Guardar en localStorage: auth_token debe ser el token FINAL (tokenDev.tokenUser) para las peticiones API
                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_token", tokenFinal);
                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_user", user);
                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_company", tokenUserResult.Company ?? "DRR Systemas");
                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_date", DateTime.UtcNow.ToString("O"));
                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_token_dev", tokenDev);
                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_token_user", tokenUserResult.TokenUser ?? "");
                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_token_final", tokenFinal);

                    return loginResponse;
                }
                else
                {
                    // Si la validación falló, devolver error
                    Console.WriteLine($"Error de autenticación: {tokenUserResult.ErrorMessage}");
                    return new LoginResponse
                    {
                        Status = "error",
                        Data = null
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en LoginAsync: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                
                return new LoginResponse
                {
                    Status = "error",
                    Data = null
                };
            }
        }

        public async Task<bool> IsLoggedInAsync()
        {
            try
            {
                var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "auth_token");
                return !string.IsNullOrEmpty(token);
            }
            catch
            {
                return false;
            }
        }

        public async Task LogoutAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "auth_token");
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "auth_user");
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "auth_company");
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "auth_date");
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "auth_token_dev");
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "auth_token_user");
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "auth_token_final");
            }
            catch
            {
                // Ignorar errores al limpiar localStorage
            }
        }

        public async Task<string?> GetTokenAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "auth_token");
            }
            catch
            {
                return null;
            }
        }

        public async Task<string?> GetUserAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "auth_user");
            }
            catch
            {
                return null;
            }
        }

        public async Task<string> GetLastRawResponseAsync()
        {
            return await Task.FromResult(_lastRawResponse);
        }

        public async Task<string> GetLastRequestInfoAsync()
        {
            return await Task.FromResult(_lastRequestInfo);
        }

        public async Task<string?> GetTokenDEVAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "auth_token_dev");
            }
            catch
            {
                return null;
            }
        }

        public async Task<string?> GetTokenUSERAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "auth_token_user");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Devuelve el token final (tokenDev.tokenUser) para Bearer. Lee auth_token_final y, si está vacío, auth_token.</summary>
        public async Task<string?> GetTokenFINALAsync()
        {
            try
            {
                var final = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "auth_token_final");
                if (!string.IsNullOrWhiteSpace(final)) return final;
                return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "auth_token");
            }
            catch
            {
                return null;
            }
        }

        public async Task<string?> GetCompanyAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "auth_company");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Paso 1 del login: obtiene TOKEN DEV desde la API.
        /// POST https://drrsystemas4.azurewebsites.net/Auth/GetTokenDeveloper
        /// Header: Authorization = Bearer empresaToken (token de empresa, ej. TOKEN_DRR)
        /// Body: { "User": user, "Pwd": password, "UsePublicLogin": false }
        /// </summary>
        private async Task<(bool Success, string? TokenDev, string? ErrorMessage)> GetTokenDeveloperAsync(string user, string password, string empresaToken)
        {
            try
            {
                var tokenDevRequest = new LoginRequest
                {
                    User = user,
                    Pwd = password,
                    UsePublicLogin = false
                };

                var json = JsonSerializer.Serialize(tokenDevRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {empresaToken}");

                _lastRequestInfo = "=== REQUEST GetTokenDeveloper ===\n";
                _lastRequestInfo += $"URL: {TOKEN_DEV_API_URL}\n";
                _lastRequestInfo += $"Authorization: Bearer {empresaToken}\n";
                _lastRequestInfo += $"Body: {json}\n";
                _lastRequestInfo += "===============================\n";

                var response = await _httpClient.PostAsync(TOKEN_DEV_API_URL, content);

                _lastRawResponse = $"Status Code: {response.StatusCode}\n";
                _lastRawResponse += $"Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"))}\n";

                var responseContent = await response.Content.ReadAsStringAsync();
                _lastRawResponse += $"Content: {responseContent}";

                if (response.IsSuccessStatusCode)
                {
                    var tokenDevResponse = JsonSerializer.Deserialize<TokenDeveloperResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (tokenDevResponse?.Status == "ok" && tokenDevResponse.Data != null && !string.IsNullOrWhiteSpace(tokenDevResponse.Data.Token))
                    {
                        return (true, tokenDevResponse.Data.Token, null);
                    }

                    return (false, null, tokenDevResponse?.Message ?? "No se pudo obtener el TOKEN DEV desde la API.");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return (false, null, $"Error al obtener TOKEN DEV: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                return (false, null, $"Excepción al obtener TOKEN DEV: {ex.Message}");
            }
        }

        /// <summary>
        /// Paso 2 del login: obtiene TOKEN USER usando el TOKEN DEV.
        /// POST https://drrsystemas4.azurewebsites.net/Auth/GetTokenUser
        /// Header: Authorization = Bearer tokenDev (el devuelto por GetTokenDeveloper)
        /// Body: { "User", "Pwd", "UsePublicLogin" }
        /// El token final para Bearer en el resto de APIs es: tokenDev + "." + tokenUser.
        /// </summary>
        private async Task<TokenUserResult> GetTokenUserAsync(string user, string password, string tokenDev)
        {
            try
            {
                // Bearer = tokenDev obtenido en el paso 1 (GetTokenDeveloper) o por override
                var tokenUserRequest = new LoginRequest
                {
                    User = user,
                    Pwd = password,
                    UsePublicLogin = false
                };

                var json = JsonSerializer.Serialize(tokenUserRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokenDev}");

                // Guardar info de la petición para debug (última llamada ejecutada)
                _lastRequestInfo = "=== REQUEST GetTokenUser ===\n";
                _lastRequestInfo += $"URL: {TOKEN_USER_API_URL}\n";
                _lastRequestInfo += $"Authorization: Bearer {tokenDev}\n";
                _lastRequestInfo += $"Body: {json}\n";
                _lastRequestInfo += "===========================\n";

                // Logs de inicio del proceso
                await _jsRuntime.InvokeVoidAsync("console.log", "🔐 === INICIO PROCESO LOGIN ===");
                await _jsRuntime.InvokeVoidAsync("console.log", $"🔐 Validando credenciales con TOKEN DEV fijo: {tokenDev}");
                await _jsRuntime.InvokeVoidAsync("console.log", $"👤 Usuario: {user}");
                await _jsRuntime.InvokeVoidAsync("console.log", $"📤 JSON enviado a GetTokenUser: {json}");
                await _jsRuntime.InvokeVoidAsync("console.log", $"🌐 URL API: {TOKEN_USER_API_URL}");

                var response = await _httpClient.PostAsync(TOKEN_USER_API_URL, content);

                // Guardar información de la respuesta para debug
                _lastRawResponse = $"Status Code: {response.StatusCode}\n";
                _lastRawResponse += $"Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"))}\n";
                
                var responseContent = await response.Content.ReadAsStringAsync();
                _lastRawResponse += $"Content: {responseContent}";
                
                // Logs detallados en consola del navegador
                await _jsRuntime.InvokeVoidAsync("console.log", "🔍 === RESPUESTA API GetTokenUser ===");
                await _jsRuntime.InvokeVoidAsync("console.log", $"📊 Status Code: {response.StatusCode}");
                await _jsRuntime.InvokeVoidAsync("console.log", $"📋 Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"))}");
                await _jsRuntime.InvokeVoidAsync("console.log", $"📄 Content: {responseContent}");
                await _jsRuntime.InvokeVoidAsync("console.log", "🔍 === FIN RESPUESTA API ===");

                if (response.IsSuccessStatusCode)
                {
                    var tokenUserResponse = JsonSerializer.Deserialize<TokenUserResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (tokenUserResponse?.Status == "ok" && tokenUserResponse.Data != null)
                    {
                        var tokenUser = tokenUserResponse.Data.Token ?? "";
                        // Token final = tokenDev + "." + tokenUser (formato Bearer para el resto de APIs)
                        var tokenFinal = $"{tokenDev}.{tokenUser}";

                        // Guardar en localStorage (LoginAsync también guarda auth_token con tokenFinal)
                        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_token_user", tokenUser);
                        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_token_final", tokenFinal);

                        Console.WriteLine($"✅ TOKEN USER guardado: {tokenUser}");
                        Console.WriteLine($"✅ TOKEN FINAL (Bearer): {tokenFinal?.Substring(0, Math.Min(40, tokenFinal?.Length ?? 0))}...");
                        Console.WriteLine($"✅ Usuario autenticado correctamente: {user}");

                        return new TokenUserResult
                        {
                            Success = true,
                            Company = tokenUserResponse.Data.EntidadSucursal?.RazonSocial ?? "DRR Systemas",
                            TokenUser = tokenUser,
                            TokenFinal = tokenFinal
                        };
                    }
                    else
                    {
                        Console.WriteLine($"❌ Error obteniendo TOKEN USER: {tokenUserResponse?.Message}");
                        return new TokenUserResult
                        {
                            Success = false,
                            ErrorMessage = tokenUserResponse?.Message ?? "Error desconocido en la validación de credenciales"
                        };
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    
                    // Logs detallados para errores en consola del navegador
                    await _jsRuntime.InvokeVoidAsync("console.log", "❌ === ERROR API GetTokenUser ===");
                    await _jsRuntime.InvokeVoidAsync("console.log", $"📊 Status Code: {response.StatusCode}");
                    await _jsRuntime.InvokeVoidAsync("console.log", $"📋 Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"))}");
                    await _jsRuntime.InvokeVoidAsync("console.log", $"📄 Error Content: {errorContent}");
                    await _jsRuntime.InvokeVoidAsync("console.log", "🔍 === FIN ERROR API ===");
                    
                    return new TokenUserResult
                    {
                        Success = false,
                        ErrorMessage = $"Error de autenticación: {response.StatusCode} - {errorContent}"
                    };
                }
            }
            catch (Exception ex)
            {
                await _jsRuntime.InvokeVoidAsync("console.log", "❌ === EXCEPCIÓN API GetTokenUser ===");
                await _jsRuntime.InvokeVoidAsync("console.log", $"📄 Error Message: {ex.Message}");
                await _jsRuntime.InvokeVoidAsync("console.log", $"📋 Stack Trace: {ex.StackTrace}");
                await _jsRuntime.InvokeVoidAsync("console.log", "🔍 === FIN EXCEPCIÓN API ===");
                
                return new TokenUserResult
                {
                    Success = false,
                    ErrorMessage = $"Error de conexión: {ex.Message}"
                };
            }
        }
    }
}
