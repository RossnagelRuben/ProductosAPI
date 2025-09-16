# Lógica de Funcionamiento - DEPOSITOS.razor

## 📋 Resumen General
La página DEPOSITOS.razor es un sistema de gestión de stock por ubicaciones que permite:
- Visualizar un árbol jerárquico de ubicaciones (Empresa > Depósito > Zona > Subzona)
- Filtrar y buscar productos por ubicación
- Editar inventarios en tiempo real
- Buscar productos por código de barras
- **NUEVO**: Escanear códigos QR para filtrar ubicaciones

## 🏗️ Estructura de Componentes

### Variables Principales
```csharp
// Estado de la aplicación
private string token = string.Empty;                    // Token de autenticación
private bool isLoading = false;                         // Estado de carga
private bool filtersExpanded = false;                   // Estado de filtros expandidos

// Árbol de ubicaciones
private List<TreeNode> rootNodes = new();               // Nodos raíz del árbol
private TreeNode? selectedNode = null;                  // Nodo actualmente seleccionado
private bool isProcessingSelection = false;             // Flag para evitar selecciones múltiples

// Productos y filtros
private List<ProductoStock> productosSeleccionados = new(); // Productos de la ubicación seleccionada
private string filtroTexto = string.Empty;              // Filtro por descripción
private string filtroRubro = string.Empty;              // Filtro por rubro/familia
private List<FamiliaItem> familias = new();             // Lista de familias disponibles

// Modales
private bool showProductoModal = false;                 // Modal de detalle de producto
private bool showTokenModal = false;                    // Modal de token de autenticación
private bool showRubroModal = false;                    // Modal de selección de rubro
private bool showColumnConfigModal = false;             // Modal de configuración de columnas

// QR Scanner (NUEVO)
private bool showQRModal = false;                       // Modal de escáner QR
private string qrDetectedValue = string.Empty;         // Valor detectado del QR
private int? qrDetectedProductoUbicacionID = null;     // ID de ubicación del QR
```

### Servicios Inyectados
```csharp
@inject UbicacionService UbicacionService              // Servicio para cargar ubicaciones
@inject TreeBuilderService TreeBuilderService          // Servicio para construir el árbol
@inject IJSRuntime JSRuntime                          // Runtime de JavaScript
@inject ProductoService ProductoService               // Servicio para productos
```

## 🔄 Flujo de Funcionamiento

### 1. Inicialización
1. **Cargar token** desde localStorage
2. **Cargar ubicaciones** usando UbicacionService
3. **Construir árbol** usando TreeBuilderService
4. **Configurar columnas** de la grilla

### 2. Selección de Ubicación
1. **Usuario hace clic** en un nodo del árbol
2. **Se ejecuta** `SelectNodeFromJS()` o `SeleccionarNodo()`
3. **Se actualiza** `selectedNode`
4. **Se cargan productos** de la ubicación seleccionada
5. **Se actualiza la grilla** con los productos

### 3. Filtrado de Productos
1. **Filtro por texto**: Busca en descripción del producto
2. **Filtro por rubro**: Filtra por familia/rubro seleccionado
3. **Aplicación de filtros**: Se ejecuta `AplicarFiltros()`

### 4. Edición de Inventario
1. **Activar modo inventario**: `ToggleInventario()`
2. **Editar cantidades**: En la grilla de productos
3. **Guardar cambios**: `GuardarProductoModal()`

## 🆕 Funcionalidad QR Scanner

### Objetivo
Permitir al usuario escanear un código QR que contenga un `ProductoUbicacionID` para:
1. Buscar automáticamente esa ubicación en el árbol
2. Seleccionar la ubicación encontrada
3. Mostrar los productos de esa ubicación

### Implementación
```csharp
// Botón QR en la interfaz
<button class="btn btn-outline-success" @onclick="AbrirQRScanner">
    <i class="oi oi-qr-code"></i>
</button>

// Modal QR simplificado
<AppModal IsOpen="showQRModal" Title="Escanear QR de Ubicación">
    <ChildContent>
        <input type="number" @bind="qrDetectedValue" placeholder="ProductoUbicacionID" />
        <button @onclick="SimularQR">Generar ID Aleatorio</button>
    </ChildContent>
    <FooterContent>
        <button @onclick="BuscarPorQR">Buscar Ubicación</button>
    </FooterContent>
</AppModal>
```

### Flujo QR
1. **Usuario hace clic** en botón QR
2. **Se abre modal** con input para ID
3. **Usuario ingresa ID** o usa simulación
4. **Se ejecuta búsqueda** `BuscarUbicacionPorID()`
5. **Se encuentra ubicación** usando `FindNodeById()`
6. **Se selecciona ubicación** usando `SelectNodeFromJS()`
7. **Se muestran productos** de la ubicación

## 🔧 Métodos Clave

### Búsqueda de Ubicación
```csharp
private TreeNode FindNodeById(List<TreeNode> nodes, int nodeId)
{
    // Busca recursivamente en el árbol por ProductoUbicacionID
    foreach (var node in nodes)
    {
        if (node.Item.ProductoUbicacionID == nodeId)
            return node;
        
        if (node.Children.Any())
        {
            var found = FindNodeById(node.Children, nodeId);
            if (found != null) return found;
        }
    }
    return null;
}
```

### Selección de Nodo
```csharp
[JSInvokable]
public async Task SelectNodeFromJS(int nodeId, string descripcion, int level, string orden)
{
    // Valida parámetros y selecciona el nodo
    // Actualiza selectedNode y carga productos
    // Actualiza la interfaz
}
```

## 🎨 Interfaz de Usuario

### Panel Izquierdo - Árbol de Ubicaciones
- **Navegación jerárquica** con expand/collapse
- **Búsqueda** de ubicaciones
- **Selección visual** del nodo activo

### Panel Derecho - Productos
- **Grilla de productos** con columnas configurables
- **Filtros** por texto y rubro
- **Edición de inventario** en modo inventario
- **Búsqueda por código de barras**

### Controles Superiores
- **Input de código de barras** con búsqueda
- **Botón QR** para escanear ubicaciones
- **Botón de inventario** para modo edición
- **Filtros expandibles**

## 🚀 Funcionalidades Futuras

### QR Scanner Avanzado
- **Cámara en tiempo real** para escaneo automático
- **Detección automática** de códigos QR
- **Validación de formato** del QR

### Mejoras de UX
- **Notificaciones toast** para feedback
- **Loading states** mejorados
- **Responsive design** optimizado

## ⚠️ Consideraciones Importantes

### Manejo de Errores
- **Try-catch** en métodos críticos
- **Validación de parámetros** antes de procesar
- **Fallbacks** para casos de error

### Performance
- **Lazy loading** de productos
- **Paginación** para grandes cantidades
- **Caching** de datos frecuentemente usados

### Seguridad
- **Validación de tokens** de autenticación
- **Sanitización** de inputs del usuario
- **Manejo seguro** de datos sensibles

