// Devuelve tamaño mostrado real (bounding rect) y tamaño natural del archivo
window.getImageSizes = (selector) => {
    try {
        const img = document.querySelector(selector);
        if (!img) {
            console.warn(`getImageSizes: Elemento no encontrado con selector: ${selector}`);
            return { clientWidth: 0, clientHeight: 0, naturalWidth: 0, naturalHeight: 0 };
        }
        
        // Verificar que la imagen esté completamente cargada
        if (!img.complete || img.naturalWidth === 0) {
            console.warn(`getImageSizes: Imagen no completamente cargada: ${selector}`);
            return { clientWidth: 0, clientHeight: 0, naturalWidth: 0, naturalHeight: 0 };
        }
        
        const r = img.getBoundingClientRect();
        return {
            clientWidth: r.width || 0,
            clientHeight: r.height || 0,
            naturalWidth: img.naturalWidth || 0,
            naturalHeight: img.naturalHeight || 0
        };
    } catch (error) {
        console.error(`getImageSizes error: ${error.message}`);
        return { clientWidth: 0, clientHeight: 0, naturalWidth: 0, naturalHeight: 0 };
    }
};

// Ajusta el SVG para que mida EXACTO lo que mide la imagen en pantalla (px)
// y define el sistema de coordenadas con el tamaño NATURAL del archivo.
window.sizeSvgOverImage = (svgSelector, cssW, cssH, viewW, viewH) => {
    try {
        const svg = document.querySelector(svgSelector);
        if (!svg) {
            console.warn(`sizeSvgOverImage: SVG no encontrado con selector: ${svgSelector}`);
            return;
        }
        
        // Validar parámetros
        if (typeof cssW !== 'number' || typeof cssH !== 'number' || 
            typeof viewW !== 'number' || typeof viewH !== 'number') {
            console.warn(`sizeSvgOverImage: Parámetros inválidos: cssW=${cssW}, cssH=${cssH}, viewW=${viewW}, viewH=${viewH}`);
            return;
        }
        
        // Validar que los valores sean positivos
        if (cssW <= 0 || cssH <= 0 || viewW <= 0 || viewH <= 0) {
            console.warn(`sizeSvgOverImage: Valores deben ser positivos: cssW=${cssW}, cssH=${cssH}, viewW=${viewW}, viewH=${viewH}`);
            return;
        }
        
        svg.style.width = cssW + "px";
        svg.style.height = cssH + "px";
        svg.setAttribute("viewBox", `0 0 ${viewW} ${viewH}`);
        
        console.log(`sizeSvgOverImage: SVG ajustado correctamente - ${cssW}x${cssH}px, viewBox: 0 0 ${viewW} ${viewH}`);
    } catch (error) {
        console.error(`sizeSvgOverImage error: ${error.message}`);
    }
};
