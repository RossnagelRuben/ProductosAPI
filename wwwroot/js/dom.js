// Devuelve tamaño mostrado real (bounding rect) y tamaño natural del archivo
window.getImageSizes = (selector) => {
    const img = document.querySelector(selector);
    if (!img) return { clientWidth: 0, clientHeight: 0, naturalWidth: 0, naturalHeight: 0 };
    const r = img.getBoundingClientRect();
    return {
        clientWidth: r.width,
        clientHeight: r.height,
        naturalWidth: img.naturalWidth,
        naturalHeight: img.naturalHeight
    };
};

// Ajusta el SVG para que mida EXACTO lo que mide la imagen en pantalla (px)
// y define el sistema de coordenadas con el tamaño NATURAL del archivo.
window.sizeSvgOverImage = (svgSelector, cssW, cssH, viewW, viewH) => {
    const svg = document.querySelector(svgSelector);
    if (!svg) return;
    svg.style.width = cssW + "px";
    svg.style.height = cssH + "px";
    svg.setAttribute("viewBox", `0 0 ${viewW} ${viewH}`);
};
