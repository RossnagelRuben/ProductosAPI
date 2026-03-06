using System.Collections.Generic;
using System.Linq;
using BlazorApp_ProductosAPI.Models.AsignarImagenes;
using Microsoft.AspNetCore.Components;

namespace BlazorApp_ProductosAPI.Components.AsignarImagenes;

/// <summary>
/// Code-behind del componente de vista en lista de productos.
/// SOLID: SRP — solo presenta datos y delega acciones al padre (Inversión de dependencias: callbacks).
/// </summary>
public partial class ProductosImagenList
{
    /// <summary>Placeholder cuando no hay imagen o falla la carga.</summary>
    private const string PlaceholderSvg = "data:image/svg+xml,%3Csvg xmlns=%22http://www.w3.org/2000/svg%22 width=%2256%22 height=%2256%22 viewBox=%220 0 56 56%22%3E%3Crect fill=%22%23e9ecef%22 width=%2256%22 height=%2256%22/%3E%3Ctext x=%2228%22 y=%2232%22 text-anchor=%22middle%22 fill=%22%23999%22 font-size=%2210%22%3ESin%3C/text%3E%3C/svg%3E";

    [Parameter] public IEnumerable<ProductoConImagenDto>? Items { get; set; }
    [Parameter] public IReadOnlySet<int>? SelectedIds { get; set; }
    [Parameter] public int CantidadPendienteGuardar { get; set; }
    [Parameter] public int CantidadObservacionesPendienteGuardar { get; set; }
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public string LoadingText { get; set; } = "Cargando productos…";
    [Parameter] public string EmptyText { get; set; } = "No hay productos. Ajusta los filtros y haz clic en Buscar.";
    [Parameter] public bool IsBulkSearching { get; set; }
    [Parameter] public int BulkSearchingCurrent { get; set; }
    [Parameter] public int BulkSearchingTotal { get; set; }
    [Parameter] public bool IsBulkGenerating { get; set; }
    [Parameter] public int BulkGeneratingCurrent { get; set; }
    [Parameter] public int BulkGeneratingTotal { get; set; }
    [Parameter] public bool IsBulkGeneratingObservaciones { get; set; }
    [Parameter] public int BulkGeneratingObservacionesCurrent { get; set; }
    [Parameter] public int BulkGeneratingObservacionesTotal { get; set; }
    [Parameter] public bool IsGuardandoTodos { get; set; }

    [Parameter] public EventCallback<ProductoConImagenDto> OnToggleSeleccion { get; set; }
    [Parameter] public EventCallback OnToggleTodos { get; set; }
    [Parameter] public EventCallback<ProductoConImagenDto> OnAbrirPreviewImagen { get; set; }
    [Parameter] public EventCallback<ProductoConImagenDto> OnAbrirModalImagen { get; set; }
    /// <summary>
    /// Callback de búsqueda de imágenes desde la lista.
    /// Actualmente apunta a la API /Integration/ImageSearch; el nombre histórico SerpApi se mantiene por compatibilidad.
    /// </summary>
    [Parameter] public EventCallback<ProductoConImagenDto> OnBuscarImagenSerpApi { get; set; }
    [Parameter] public EventCallback<ProductoConImagenDto> OnAbrirModalObservaciones { get; set; }
    [Parameter] public EventCallback OnBuscarImagenMasivoSerpApi { get; set; }
    [Parameter] public EventCallback OnGenerarImagenMasivo { get; set; }
    [Parameter] public EventCallback OnGenerarObservacionesMasivo { get; set; }
    /// <summary>Generar observaciones con OpenAI (Chat) para los seleccionados.</summary>
    [Parameter] public EventCallback OnGenerarObservacionesMasivoOpenAI { get; set; }
    [Parameter] public EventCallback OnGuardarCambios { get; set; }

    /// <summary>URL para miniaturas; si no hay imagen real se usa placeholder.</summary>
    [Parameter] public string PlaceholderImageUrl { get; set; } = PlaceholderSvg;

    private bool TieneSeleccionados => SelectedIds != null && SelectedIds.Count > 0;
    private int SelectedCount => SelectedIds?.Count ?? 0;
    private int TotalPendienteGuardar => CantidadPendienteGuardar + CantidadObservacionesPendienteGuardar;

    private bool TodosSeleccionados
    {
        get
        {
            if (Items == null || !Items.Any()) return false;
            var list = Items.ToList();
            return SelectedIds != null && list.All(p => SelectedIds.Contains(p.ProductoID));
        }
    }

    private bool IsSelected(int productoId) => SelectedIds != null && SelectedIds.Contains(productoId);

    private static bool PuedeBuscarEnWeb(ProductoConImagenDto p) =>
        !string.IsNullOrWhiteSpace(p.DescripcionLarga);

    private bool TieneImagenReal(ProductoConImagenDto p)
    {
        if (string.IsNullOrWhiteSpace(p.ImagenUrl)) return false;
        if (p.ImagenUrl.StartsWith("data:image/svg", StringComparison.OrdinalIgnoreCase)) return false;
        if (EsPdf(p.ImagenUrl)) return false;
        return true;
    }

    private string GetThumbSrc(ProductoConImagenDto p)
    {
        if (string.IsNullOrWhiteSpace(p.ImagenUrl)) return PlaceholderImageUrl;
        if (EsPdf(p.ImagenUrl)) return PlaceholderImageUrl;
        return p.ImagenUrl;
    }

    private static bool EsPdf(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (url.StartsWith("data:application/pdf", StringComparison.OrdinalIgnoreCase)) return true;
        if (url.IndexOf(".pdf", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    /// <summary>Maneja el cambio del checkbox "Seleccionar todos"; delega al padre.</summary>
    private async void OnToggleTodosClick(ChangeEventArgs _)
    {
        await OnToggleTodos.InvokeAsync();
    }

    /// <summary>Invoca la búsqueda masiva de imágenes (API integración) y espera al padre para que se ejecute en el contexto correcto.</summary>
    private async Task OnBuscarImagenMasivoClickAsync()
    {
        await OnBuscarImagenMasivoSerpApi.InvokeAsync();
    }
}
