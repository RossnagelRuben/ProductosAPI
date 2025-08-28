// Modal Helper para Blazor - Sistema robusto de modales
window.appModal = {
    currentModal: null,
    previousActiveElement: null,
    focusableElements: null,
    firstFocusableElement: null,
    lastFocusableElement: null,
    
    // Inicializar modal con focus trap y scroll lock
    init: function(modalElement, disableEsc = false) {
        this.currentModal = modalElement;
        this.previousActiveElement = document.activeElement;
        
        // Scroll lock
        document.body.style.overflow = 'hidden';
        document.body.style.paddingRight = this.getScrollbarWidth() + 'px';
        
        // Obtener elementos focusables
        this.updateFocusableElements();
        
        // Focus en el primer elemento focusable
        if (this.firstFocusableElement) {
            this.firstFocusableElement.focus();
        }
        
        // Event listeners para focus trap
        this.setupFocusTrap();
        
        // ESC key handler
        if (!disableEsc) {
            this.setupEscHandler();
        }
        
        // Log para debugging
        console.log('Modal initialized with focus trap');
    },
    
    // Destruir modal y restaurar estado
    destroy: function() {
        // Restaurar scroll
        document.body.style.overflow = '';
        document.body.style.paddingRight = '';
        
        // Restaurar focus
        if (this.previousActiveElement && this.previousActiveElement.focus) {
            this.previousActiveElement.focus();
        }
        
        // Limpiar event listeners
        this.cleanupEventListeners();
        
        this.currentModal = null;
        this.previousActiveElement = null;
        this.focusableElements = null;
        this.firstFocusableElement = null;
        this.lastFocusableElement = null;
        
        console.log('Modal destroyed and state restored');
    },
    
    // Obtener elementos focusables dentro del modal
    updateFocusableElements: function() {
        if (!this.currentModal) return;
        
        const focusableSelectors = [
            'button:not([disabled])',
            'input:not([disabled])',
            'select:not([disabled])',
            'textarea:not([disabled])',
            'a[href]',
            '[tabindex]:not([tabindex="-1"])',
            '[contenteditable="true"]'
        ];
        
        this.focusableElements = Array.from(
            this.currentModal.querySelectorAll(focusableSelectors.join(','))
        ).filter(el => {
            const style = window.getComputedStyle(el);
            return style.display !== 'none' && style.visibility !== 'hidden';
        });
        
        this.firstFocusableElement = this.focusableElements[0] || null;
        this.lastFocusableElement = this.focusableElements[this.focusableElements.length - 1] || null;
    },
    
    // Configurar focus trap
    setupFocusTrap: function() {
        if (!this.currentModal) return;
        
        this.currentModal.addEventListener('keydown', this.handleKeyDown.bind(this));
    },
    
    // Configurar handler para ESC
    setupEscHandler: function() {
        if (!this.currentModal) return;
        
        this.currentModal.addEventListener('keydown', this.handleEscKey.bind(this));
    },
    
    // Manejar teclas para focus trap
    handleKeyDown: function(event) {
        if (event.key !== 'Tab') return;
        
        if (event.shiftKey) {
            // Shift + Tab
            if (document.activeElement === this.firstFocusableElement) {
                event.preventDefault();
                this.lastFocusableElement?.focus();
            }
        } else {
            // Tab
            if (document.activeElement === this.lastFocusableElement) {
                event.preventDefault();
                this.firstFocusableElement?.focus();
            }
        }
    },
    
    // Manejar ESC key
    handleEscKey: function(event) {
        if (event.key === 'Escape') {
            // Disparar evento personalizado para que Blazor lo maneje
            const escEvent = new CustomEvent('modal-escape', { bubbles: true });
            this.currentModal.dispatchEvent(escEvent);
        }
    },
    
    // Limpiar event listeners
    cleanupEventListeners: function() {
        if (!this.currentModal) return;
        
        this.currentModal.removeEventListener('keydown', this.handleKeyDown.bind(this));
        this.currentModal.removeEventListener('keydown', this.handleEscKey.bind(this));
    },
    
    // Calcular ancho de scrollbar para evitar saltos
    getScrollbarWidth: function() {
        const outer = document.createElement('div');
        outer.style.visibility = 'hidden';
        outer.style.overflow = 'scroll';
        document.body.appendChild(outer);
        
        const inner = document.createElement('div');
        outer.appendChild(inner);
        
        const scrollbarWidth = outer.offsetWidth - inner.offsetWidth;
        outer.parentNode.removeChild(outer);
        
        return scrollbarWidth;
    },
    
    // Método para actualizar elementos focusables (útil cuando el contenido cambia)
    refreshFocusableElements: function() {
        this.updateFocusableElements();
    }
};

// Función global para focus por ID (mantener compatibilidad)
window.blazorFocusById = function(id) {
    const el = document.getElementById(id);
    if (el) {
        el.focus();
        if (el.select) {
            try { el.select(); } catch { }
        }
    }
};

// Función para descargar texto como archivo
window.blazorDownloadText = function(filename, base64) {
    const link = document.createElement('a');
    link.href = 'data:text/plain;base64,' + base64;
    link.download = filename || 'archivo.txt';
    document.body.appendChild(link);
    link.click();
    setTimeout(() => link.remove(), 0);
};

// Función para imprimir/exportar a PDF
window.blazorPrint = function(containerId) {
    const el = containerId ? document.getElementById(containerId) : null;
    if (!el) { window.print(); return; }

    // Inyecta CSS de impresión para mostrar únicamente el contenedor
    const style = document.createElement('style');
    style.type = 'text/css';
    style.setAttribute('data-blazor-print', 'true');
    style.innerHTML = `@media print { body * { visibility: hidden !important; } #${containerId}, #${containerId} * { visibility: visible !important; } #${containerId} { position: absolute; left: 0; top: 0; width: 100%; } }`;
    document.head.appendChild(style);

    try { window.print(); }
    finally {
        setTimeout(() => { if (style && style.parentNode) style.parentNode.removeChild(style); }, 0);
    }
};
