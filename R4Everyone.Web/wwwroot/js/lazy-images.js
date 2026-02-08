window.r4eLazyImages = (() => {
    const observedElements = new WeakSet();
    let observer;

    const ensureObserver = (dotNetRef) => {
        if (observer) {
            return observer;
        }

        observer = new IntersectionObserver((entries) => {
            for (const entry of entries) {
                if (!entry.isIntersecting) {
                    continue;
                }

                const id = entry.target.dataset.gameId;
                if (id) {
                    dotNetRef.invokeMethodAsync("NotifyVisible", id);
                }

                observer.unobserve(entry.target);
                observedElements.delete(entry.target);
            }
        }, {
            rootMargin: "0px 0px 100px 0px",
            threshold: 0.01
        });

        return observer;
    };

    const observeAll = (dotNetRef) => {
        const currentObserver = ensureObserver(dotNetRef);
        const candidates = document.querySelectorAll("[data-game-id]");
        for (const element of candidates) {
            if (observedElements.has(element)) {
                continue;
            }

            observedElements.add(element);
            currentObserver.observe(element);
        }
    };

    return { observeAll };
})();
