using BlazorApp_ProductosAPI.Models;
using Microsoft.JSInterop;
using System.Text;
using System.Text.Json;

namespace BlazorApp_ProductosAPI.Services
{
    public interface IAuthService
    {
        Task<LoginResponse?> LoginAsync(string user, string password);
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
        private const string API_URL = "https://drrsystemas4.azurewebsites.net/Auth/GetTokenDeveloper";
        private const string TOKEN_USER_API_URL = "https://drrsystemas4.azurewebsites.net/Auth/GetTokenUser";
        private const string TOKEN_DRR = "4a7183cf-9515-4d87-a9f1-a9e1f952cc7c";
        private string _lastRawResponse = string.Empty;
        private string _lastRequestInfo = string.Empty;

        public AuthService(HttpClient httpClient, IJSRuntime jsRuntime)
        {
            _httpClient = httpClient;
            _jsRuntime = jsRuntime;
        }

        public async Task<LoginResponse?> LoginAsync(string user, string password)
        {
            try
            {
                var loginRequest = new LoginRequest
                {
                    User = user,
                    Pwd = password,
                    UsePublicLogin = false
                };

                var json = JsonSerializer.Serialize(loginRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Log detallado del JSON enviado
                Console.WriteLine($"JSON enviado: {json}");
                Console.WriteLine($"Content-Type: {content.Headers.ContentType}");
                Console.WriteLine($"Content-Length: {content.Headers.ContentLength}");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {TOKEN_DRR}");

                // Capturar información detallada de la petición
                var requestInfo = $"📤 PETICIÓN DETALLADA:\n";
                requestInfo += $"URL: {API_URL}\n";
                requestInfo += $"Method: POST\n";
                requestInfo += $"JSON: {json}\n";
                requestInfo += $"Content-Type: {content.Headers.ContentType}\n";
                requestInfo += $"Content-Length: {content.Headers.ContentLength}\n";
                requestInfo += $"Headers:\n";
                foreach (var header in _httpClient.DefaultRequestHeaders)
                {
                    requestInfo += $"  {header.Key}: {string.Join(", ", header.Value)}\n";
                }
                requestInfo += $"Authorization Bearer: {TOKEN_DRR}\n";
                _lastRequestInfo = requestInfo;

                // Log de headers para debugging
                Console.WriteLine($"Headers enviados:");
                foreach (var header in _httpClient.DefaultRequestHeaders)
                {
                    Console.WriteLine($"{header.Key}: {string.Join(", ", header.Value)}");
                }
                Console.WriteLine($"Token DRR: {TOKEN_DRR}");
                Console.WriteLine($"URL de la API: {API_URL}");

                var response = await _httpClient.PostAsync(API_URL, content);

                // Log del status code
                Console.WriteLine($"Response Status Code: {response.StatusCode}");
                Console.WriteLine($"Response Headers:");
                foreach (var header in response.Headers)
                {
                    Console.WriteLine($"{header.Key}: {string.Join(", ", header.Value)}");
                }

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _lastRawResponse = responseContent;
                    
                    // Log de la respuesta cruda para debugging
                    Console.WriteLine($"API Response Raw: {responseContent}");
                    
                    try
                    {
                        var loginResponse = JsonSerializer.Deserialize<LoginResponse>(responseContent, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (loginResponse?.Status == "ok" && loginResponse.Data != null)
                        {
                            // Guardar datos básicos en localStorage
                            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_token", loginResponse.Data.Token);
                            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_user", loginResponse.Data.User);
                            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_company", loginResponse.Data.Company);
                            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_date", loginResponse.Data.DateUTC.ToString("O"));
                            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_token_dev", loginResponse.Data.Token);
                            
                            // Obtener TOKEN USER usando el TOKEN DEV
                            await GetTokenUserAsync(user, password, loginResponse.Data.Token);
                        }

                        return loginResponse;
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"JSON Deserialization Error: {ex.Message}");
                        Console.WriteLine($"Raw Response: {responseContent}");
                        
                        // Si no se puede deserializar, crear una respuesta de error
                        return new LoginResponse
                        {
                            Status = "error",
                            Data = null
                        };
                    }
                }
                else
                {
                    // Si la respuesta no es exitosa, crear una respuesta de error
                    return new LoginResponse
                    {
                        Status = "error",
                        Data = null
                    };
                }
            }
            catch (Exception ex)
            {
                // En caso de excepción, devolver una respuesta de error
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

        public async Task<string?> GetTokenFINALAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "auth_token_final");
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

        private async Task GetTokenUserAsync(string user, string password, string tokenDev)
        {
            try
            {
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

                Console.WriteLine($"Obteniendo TOKEN USER con TOKEN DEV: {tokenDev}");
                Console.WriteLine($"JSON enviado a GetTokenUser: {json}");

                var response = await _httpClient.PostAsync(TOKEN_USER_API_URL, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"TOKEN USER API Response: {responseContent}");

                    var tokenUserResponse = JsonSerializer.Deserialize<TokenUserResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (tokenUserResponse?.Status == "ok" && tokenUserResponse.Data != null)
                    {
                        var tokenUser = tokenUserResponse.Data.Token;
                        var tokenFinal = $"{tokenDev}.{tokenUser}";

                        // Guardar TOKEN USER y TOKEN FINAL
                        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_token_user", tokenUser);
                        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_token_final", tokenFinal);

                        Console.WriteLine($"TOKEN USER guardado: {tokenUser}");
                        Console.WriteLine($"TOKEN FINAL guardado: {tokenFinal}");
                    }
                    else
                    {
                        Console.WriteLine($"Error obteniendo TOKEN USER: {tokenUserResponse?.Message}");
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error en API GetTokenUser: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Excepción obteniendo TOKEN USER: {ex.Message}");
            }
        }
    }
}
