// JavaScript simplificado para DEPOSITOS - SIN BUCLES INFINITOS

// Función para verificar si es dispositivo móvil
window.isMobileDevice = function() {
    return window.innerWidth <= 768;
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

console.log('JavaScript simplificado cargado correctamente');