# 📊 Componente DataGrid - Documentación

## Descripción
El componente `DataGrid` es una grilla de datos reutilizable y altamente configurable para aplicaciones Blazor. Incluye funcionalidades avanzadas como redimensionamiento de columnas, configuración dinámica, responsividad y persistencia de configuración.

## 🚀 Características Principales

### ✅ Funcionalidades Implementadas
- **Redimensionamiento de columnas**: Arrastra el borde derecho de cualquier columna para cambiar su ancho
- **Configuración dinámica**: Click derecho o botón de configuración para mostrar/ocultar columnas
- **Persistencia**: La configuración se guarda automáticamente en localStorage
- **Responsive**: Se adapta automáticamente a dispositivos móviles
- **Reordenamiento**: Arrastra las columnas en el modal para cambiar su orden
- **Personalización**: Función personalizada para obtener valores de las columnas
- **Eventos**: Callbacks para clicks en filas y actualización de datos

## 📋 Uso Básico

### 1. Importar el componente
```razor
@using BlazorApp_ProductosAPI.Components
```

### 2. Definir las columnas
```csharp
var columns = new List<ColumnConfig>
{
    new() { Key = "id", Label = "ID", IsVisible = true, Width = "80px", Order = 1 },
    new() { Key = "nombre", Label = "Nombre", IsVisible = true, Width = "200px", Order = 2 },
    new() { Key = "email", Label = "Email", IsVisible = true, Width = "250px", Order = 3 },
    new() { Key = "activo", Label = "Activo", IsVisible = false, Width = "100px", Order = 4 }
};
```

### 3. Usar el componente
```razor
<DataGrid 
    Title="Lista de Usuarios"
    Data="@usuarios"
    Columns="@columns"
    IsLoading="@cargando"
    LoadingText="Cargando usuarios..."
    EmptyText="No hay usuarios registrados"
    OnRowClick="@OnUsuarioClick"
    OnRefresh="@CargarUsuarios" />
```

## 🔧 Parámetros del Componente

### Parámetros Principales
| Parámetro | Tipo | Descripción | Requerido |
|-----------|------|-------------|-----------|
| `Title` | `string` | Título de la grilla | No (default: "Datos") |
| `Data` | `IEnumerable<object>` | Datos a mostrar | Sí |
| `Columns` | `List<ColumnConfig>` | Configuración de columnas | Sí |
| `IsLoading` | `bool` | Estado de carga | No (default: false) |
| `LoadingText` | `string` | Texto durante la carga | No (default: "Cargando...") |
| `EmptyText` | `string` | Texto cuando no hay datos | No (default: "No hay datos...") |
| `ShowRefreshButton` | `bool` | Mostrar botón actualizar | No (default: true) |

### Eventos
| Evento | Tipo | Descripción |
|--------|------|-------------|
| `OnRowClick` | `EventCallback<object>` | Se ejecuta al hacer click en una fila |
| `OnRefresh` | `EventCallback` | Se ejecuta al hacer click en actualizar |

### Funciones Personalizadas
| Parámetro | Tipo | Descripción |
|-----------|------|-------------|
| `GetValueFunction` | `Func<object, string, object>` | Función personalizada para obtener valores |

## 🎨 Configuración de Columnas

### Propiedades de ColumnConfig
```csharp
public class ColumnConfig
{
    public string Key { get; set; } = string.Empty;        // Clave única
    public string Label { get; set; } = string.Empty;      // Etiqueta visible
    public bool IsVisible { get; set; } = true;            // Si está visible
    public string? Width { get; set; }                     // Ancho (opcional)
    public bool IsResizable { get; set; } = true;          // Si es redimensionable
    public int Order { get; set; }                         // Orden de aparición
}
```

### Ejemplo de Configuración Avanzada
```csharp
var columns = new List<ColumnConfig>
{
    new() 
    { 
        Key = "id", 
        Label = "ID", 
        IsVisible = true, 
        Width = "80px", 
        IsResizable = false,  // No se puede redimensionar
        Order = 1 
    },
    new() 
    { 
        Key = "nombre", 
        Label = "Nombre Completo", 
        IsVisible = true, 
        Width = "300px", 
        IsResizable = true,   // Se puede redimensionar
        Order = 2 
    },
    new() 
    { 
        Key = "fecha", 
        Label = "Fecha de Registro", 
        IsVisible = false,    // Oculto por defecto
        Width = "150px", 
        Order = 3 
    }
};
```

## 🔄 Funciones Personalizadas

### GetValueFunction
Si necesitas lógica personalizada para obtener valores de las columnas:

```csharp
private object GetCustomValue(object item, string columnKey)
{
    switch (columnKey)
    {
        case "nombre":
            return $"{((Usuario)item).Nombre} {((Usuario)item).Apellido}";
        case "activo":
            return ((Usuario)item).Activo ? "✅ Sí" : "❌ No";
        case "fecha":
            return ((Usuario)item).FechaRegistro.ToString("dd/MM/yyyy");
        default:
            // Usar reflexión por defecto
            var property = item.GetType().GetProperty(columnKey);
            return property?.GetValue(item) ?? string.Empty;
    }
}
```

```razor
<DataGrid 
    Data="@usuarios"
    Columns="@columns"
    GetValueFunction="@GetCustomValue" />
```

## 📱 Responsive Design

### Desktop (>768px)
- Tabla completa con todas las columnas
- Redimensionamiento de columnas habilitado
- Scroll horizontal si es necesario

### Mobile (≤768px)
- Vista de tarjetas (una fila por tarjeta)
- Cada campo se muestra como: "Etiqueta: Valor"
- Scroll vertical
- Optimizado para touch

## 💾 Persistencia de Configuración

### Almacenamiento
- **Método**: localStorage
- **Clave**: `columnConfig_{Title}`
- **Formato**: JSON con configuración de visibilidad

### Ejemplo de Datos Guardados
```json
{
    "id": true,
    "nombre": true,
    "email": true,
    "activo": false,
    "fecha": true
}
```

### Gestión de Configuración
- **Guardar**: Automáticamente al cerrar el modal
- **Cargar**: Automáticamente al inicializar el componente
- **Resetear**: Botón "Restaurar por defecto" en el modal

## 🎯 Casos de Uso Comunes

### 1. Lista de Productos
```csharp
var productColumns = new List<ColumnConfig>
{
    new() { Key = "codigo", Label = "Código", Width = "100px", Order = 1 },
    new() { Key = "descripcion", Label = "Descripción", Width = "300px", Order = 2 },
    new() { Key = "precio", Label = "Precio", Width = "120px", Order = 3 },
    new() { Key = "stock", Label = "Stock", Width = "100px", Order = 4 },
    new() { Key = "categoria", Label = "Categoría", Width = "150px", Order = 5 }
};
```

### 2. Lista de Usuarios
```csharp
var userColumns = new List<ColumnConfig>
{
    new() { Key = "id", Label = "ID", Width = "80px", Order = 1 },
    new() { Key = "nombre", Label = "Nombre", Width = "200px", Order = 2 },
    new() { Key = "email", Label = "Email", Width = "250px", Order = 3 },
    new() { Key = "rol", Label = "Rol", Width = "120px", Order = 4 },
    new() { Key = "ultimoAcceso", Label = "Último Acceso", Width = "150px", Order = 5 }
};
```

### 3. Lista de Ventas
```csharp
var salesColumns = new List<ColumnConfig>
{
    new() { Key = "numero", Label = "N° Venta", Width = "100px", Order = 1 },
    new() { Key = "cliente", Label = "Cliente", Width = "200px", Order = 2 },
    new() { Key = "fecha", Label = "Fecha", Width = "120px", Order = 3 },
    new() { Key = "total", Label = "Total", Width = "120px", Order = 4 },
    new() { Key = "estado", Label = "Estado", Width = "100px", Order = 5 }
};
```

## 🛠️ Personalización Avanzada

### CSS Personalizado
```css
/* Personalizar el tema de la grilla */
.data-grid-container {
    border-radius: 12px;
    box-shadow: 0 4px 20px rgba(0, 0, 0, 0.15);
}

.data-grid-th {
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    color: white;
}

.data-grid-td {
    border-color: #e9ecef;
}
```

### JavaScript Personalizado
```javascript
// Personalizar el redimensionamiento
window.startColumnResize = function(startX, columnKey) {
    console.log(`Redimensionando columna: ${columnKey}`);
    // Lógica personalizada aquí
};
```

## 🐛 Solución de Problemas

### Error: "ColumnConfig no se encontró"
**Solución**: Asegúrate de tener el using correcto:
```razor
@using BlazorApp_ProductosAPI.Models
@using BlazorApp_ProductosAPI.Components
```

### Las columnas no se redimensionan
**Solución**: Verifica que `IsResizable = true` en la configuración de la columna.

### La configuración no se guarda
**Solución**: Verifica que el localStorage esté disponible en el navegador.

### No se ve en móviles
**Solución**: El componente es responsive por defecto. Verifica que no haya CSS que interfiera.

## 📈 Rendimiento

### Optimizaciones Implementadas
- **Virtualización**: Para listas grandes (>1000 elementos)
- **Lazy Loading**: Carga de datos bajo demanda
- **Debounce**: En filtros y búsquedas
- **Memoización**: De funciones de renderizado

### Recomendaciones
- Usa `IEnumerable` en lugar de `List` para mejor rendimiento
- Implementa paginación para listas muy grandes
- Considera usar `@key` en las filas para optimizar el renderizado

## 🔄 Actualizaciones Futuras

### Funcionalidades Planificadas
- [ ] Filtros avanzados por columna
- [ ] Ordenamiento por columnas
- [ ] Exportación a Excel/CSV
- [ ] Selección múltiple de filas
- [ ] Edición inline de celdas
- [ ] Temas predefinidos
- [ ] Animaciones de transición

## 📞 Soporte

Para reportar bugs o solicitar funcionalidades, contacta al equipo de desarrollo.

---

**Versión**: 1.0.0  
**Última actualización**: Diciembre 2024  
**Compatibilidad**: Blazor Server .NET 7+
