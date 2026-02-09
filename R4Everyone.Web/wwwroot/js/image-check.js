window.r4eImages = (() => {
    const check = (url) => new Promise((resolve) => {
        const img = new Image();
        const finalize = (result) => {
            img.onload = null;
            img.onerror = null;
            resolve(result);
        };

        img.onload = () => finalize(true);
        img.onerror = () => finalize(false);
        img.src = url;
    });

    return { check };
})();
