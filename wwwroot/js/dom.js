// JavaScript simplificado para DEPOSITOS - SIN BUCLES INFINITOS

// Función para verificar si es dispositivo móvil
window.isMobileDevice = function() {
    return window.innerWidth <= 768;
};

// Función para actualizar el estado de móvil en Blazor
window.updateMobileState = function(dotNetRef) {
    const isMobile = window.innerWidth <= 768;
    console.log(`📱 Actualizando estado móvil: ${isMobile} (ancho: ${window.innerWidth})`);
    
    // Guardar referencia de Blazor para uso posterior
    window.blazorRef = dotNetRef;
    
    dotNetRef.invokeMethodAsync('SetMobileState', isMobile);
};

// Función para configurar el listener de resize
window.setupResizeListener = function(dotNetRef) {
    console.log('🔧 Configurando listener de resize...');
    
    // Guardar referencia de Blazor
    window.blazorRef = dotNetRef;
    
    // Remover listener anterior si existe
    if (window.resizeListener) {
        window.removeEventListener('resize', window.resizeListener);
    }
    
    // Crear nuevo listener
    window.resizeListener = function() {
        clearTimeout(window.resizeTimeout);
        window.resizeTimeout = setTimeout(() => {
            const currentWidth = window.innerWidth;
            const isMobile = currentWidth <= 768;
            const wasMobile = window.lastWidth <= 768;
            
            console.log(`📱 Resize detectado - ancho: ${currentWidth}, es móvil: ${isMobile}, era móvil: ${wasMobile}`);
            
            // Actualizar estado móvil
            if (window.blazorRef) {
                window.updateMobileState(window.blazorRef);
                
                // Si cambió a móvil, forzar desactivación de virtualización
                if (isMobile && !wasMobile) {
                    console.log("📱 Cambio a móvil detectado - Forzando desactivación de virtualización");
                    setTimeout(() => {
                        if (window.blazorRef) {
                            window.blazorRef.invokeMethodAsync('ForceDisableVirtualization');
                            // Forzar recarga de productos si hay productos cargados
                            setTimeout(() => {
                                if (window.blazorRef) {
                                    window.blazorRef.invokeMethodAsync('ForceReloadAllProducts');
                                }
                            }, 500);
                        }
                    }, 200);
                }
            }
            
            window.lastWidth = currentWidth;
        }, 150);
    };
    
    // Agregar listener
    window.addEventListener('resize', window.resizeListener);
    
    // Inicializar ancho anterior
    window.lastWidth = window.innerWidth;
    
    console.log('✅ Listener de resize configurado correctamente');
};

// Función para detectar el estado móvil inicial
window.detectInitialMobileState = function(dotNetRef) {
    console.log('🔍 Detectando estado móvil inicial...');
    
    const isMobile = window.innerWidth <= 768;
    console.log(`📱 Estado móvil inicial: ${isMobile} (ancho: ${window.innerWidth})`);
    
    // Guardar referencia de Blazor
    window.blazorRef = dotNetRef;
    
    // Actualizar estado inmediatamente
    window.updateMobileState(dotNetRef);
    
    // Si es móvil desde el inicio, forzar desactivación de virtualización
    if (isMobile) {
        console.log("📱 Pantalla móvil detectada desde el inicio - Forzando desactivación de virtualización");
        setTimeout(() => {
            if (window.blazorRef) {
                window.blazorRef.invokeMethodAsync('ForceDisableVirtualization');
            }
        }, 500);
    }
};

// Función para enfocar elementos por ID
window.blazorFocusById = function(id) {
    const element = document.getElementById(id);
    if (element) {
        element.focus();
    }
};

// Función para asegurar focus en móviles
window.ensureBarcodeFocus = function() {
    const barcodeInput = document.getElementById('barcodeInput');
    if (barcodeInput) {
        barcodeInput.focus();
    }
};

// Función para seleccionar texto en la modal de stock (especialmente para móviles)
window.selectTextInModal = function() {
    const stockInput = document.getElementById('modalStockActual');
    if (stockInput) {
        stockInput.focus();
        stockInput.select(); // Seleccionar todo el texto
    }
};

// Función para ocultar elementos por ID
window.hideById = function(id) {
    const element = document.getElementById(id);
    if (element) {
        element.style.display = 'none';
    }
};

// Función para ocultar elementos por clase
window.hideByClass = function(className) {
    const elements = document.getElementsByClassName(className);
    for (let i = 0; i < elements.length; i++) {
        elements[i].style.display = 'none';
    }
};

// Función para mostrar elementos por ID
window.showById = function(id) {
    const element = document.getElementById(id);
    if (element) {
        element.style.display = 'block';
    }
};

// Función para mostrar elementos por clase
window.showByClass = function(className) {
    const elements = document.getElementsByClassName(className);
    for (let i = 0; i < elements.length; i++) {
        elements[i].style.display = 'block';
    }
};

// Función para ajustar layout responsivo - SIMPLIFICADA
window.adjustResponsiveLayout = function() {
    try {
        const windowWidth = window.innerWidth;
        const windowHeight = window.innerHeight;
        
        console.log(`Dimensiones de ventana: ${windowWidth}x${windowHeight}`);
        
        // Verificar si estamos en el panel de productos
        const rightPanel = document.querySelector('.right-panel');
        const leftPanel = document.querySelector('.left-panel');
        
        if (rightPanel && !rightPanel.classList.contains('panel-hidden')) {
            // Si estamos en el panel de productos, mantenerlo visible y ocultar ubicaciones
            console.log('Manteniendo panel de productos visible');
            rightPanel.style.display = 'flex';
            rightPanel.style.visibility = 'visible';
            rightPanel.style.opacity = '1';
            
            if (leftPanel) {
                leftPanel.style.display = 'none';
                leftPanel.classList.add('panel-hidden');
            }
        }
        
        // Solo ajustar si es necesario
        if (windowWidth <= 768) {
            // Móvil: asegurar que los paneles usen 100% del ancho
            const container = document.querySelector('.depositos-container');
            if (container) {
                container.style.width = '100vw';
                container.style.maxWidth = '100vw';
            }
        }
        
        console.log('Layout responsivo ajustado correctamente');
        
    } catch (error) {
        console.error('Error ajustando layout responsivo:', error);
    }
};

// Inicializar cuando el DOM esté listo
document.addEventListener('DOMContentLoaded', function() {
    try {
        console.log('Inicializando JavaScript simplificado...');
        
        // Solo ajustar layout inicial
        adjustResponsiveLayout();
        
        console.log('JavaScript inicializado correctamente');
        
    } catch (error) {
        console.error('Error inicializando JavaScript:', error);
    }
});

// Listener para cambios de tamaño de ventana - SIMPLIFICADO
window.addEventListener('resize', function() {
    setTimeout(() => {
        adjustResponsiveLayout();
    }, 100);
});

// Listener de resize ya está configurado en setupResizeListener

// Funciones para Imagen.razor - OCR y overlay SVG

// Función para obtener dimensiones de imagen
window.getImageSizes = function(selector) {
    try {
        const img = document.querySelector(selector);
        if (!img) {
            console.warn(`No se encontró imagen con selector: ${selector}`);
            return {
                clientWidth: 0,
                clientHeight: 0,
                naturalWidth: 0,
                naturalHeight: 0
            };
        }
        
        return {
            clientWidth: img.clientWidth || 0,
            clientHeight: img.clientHeight || 0,
            naturalWidth: img.naturalWidth || 0,
            naturalHeight: img.naturalHeight || 0
        };
    } catch (error) {
        console.error('Error en getImageSizes:', error);
        return {
            clientWidth: 0,
            clientHeight: 0,
            naturalWidth: 0,
            naturalHeight: 0
        };
    }
};

// Función para ajustar SVG sobre imagen
window.sizeSvgOverImage = function(selector, clientW, clientH, naturalW, naturalH) {
    try {
        const svg = document.querySelector(selector);
        if (!svg) {
            console.warn(`No se encontró SVG con selector: ${selector}`);
            return;
        }
        
        // Asegurar que los valores sean válidos
        if (!clientW || !clientH || !naturalW || !naturalH) {
            console.warn('Dimensiones inválidas para sizeSvgOverImage');
            return;
        }
        
        svg.setAttribute('width', clientW);
        svg.setAttribute('height', clientH);
        svg.setAttribute('viewBox', `0 0 ${naturalW} ${naturalH}`);
        svg.setAttribute('preserveAspectRatio', 'none');
        
    } catch (error) {
        console.error('Error en sizeSvgOverImage:', error);
    }
};

console.log('JavaScript simplificado cargado correctamente');