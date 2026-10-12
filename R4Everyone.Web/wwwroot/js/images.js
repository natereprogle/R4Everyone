window.r4eImages = (() => {
    // Resolves true if the image loads, false if the browser reports an error. Never rejects.
    const exists = (url) => new Promise((resolve) => {
        const img = new Image();
        img.onload = () => resolve(true);
        img.onerror = () => resolve(false);
        img.src = url;
    });

    return { exists };
})();
