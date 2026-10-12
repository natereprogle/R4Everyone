window.r4eLazyImages = (() => {
    let observer;
    let currentRef;

    const notifyVisible = (gameId) => {
        if (!currentRef) {
            return;
        }

        currentRef.invokeMethodAsync("NotifyVisible", gameId)
            .catch((error) => console.error(`R4Everyone: failed to load cover art for ${gameId}.`, error));
    };

    const ensureObserver = () => {
        observer ??= new IntersectionObserver((entries) => {
            for (const entry of entries) {
                if (!entry.isIntersecting) {
                    continue;
                }

                observer.unobserve(entry.target);
                const id = entry.target.dataset.gameId;
                if (id) {
                    notifyVisible(id);
                }
            }
        }, {
            rootMargin: "0px 0px 100px 0px",
            threshold: 0.01
        });

        return observer;
    };

    // Observing an element twice is a no-op, so this can run after every render.
    const observeAll = (dotNetRef) => {
        currentRef = dotNetRef;
        const activeObserver = ensureObserver();
        for (const element of document.querySelectorAll("[data-game-id]")) {
            activeObserver.observe(element);
        }
    };

    const disconnect = () => {
        observer?.disconnect();
        observer = undefined;
        currentRef = undefined;
    };

    return { observeAll, disconnect };
})();
