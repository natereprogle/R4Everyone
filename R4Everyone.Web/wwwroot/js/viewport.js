window.r4eViewport = (() => {
    let onResize;

    const register = (dotNetRef) => {
        unregister();
        onResize = () => {
            dotNetRef.invokeMethodAsync("OnResize", window.innerWidth, window.innerHeight)
                .catch((error) => console.error("R4Everyone: failed to report the window size.", error));
        };

        onResize();
        window.addEventListener("resize", onResize);
    };

    const unregister = () => {
        if (onResize) {
            window.removeEventListener("resize", onResize);
            onResize = undefined;
        }
    };

    return { register, unregister };
})();
