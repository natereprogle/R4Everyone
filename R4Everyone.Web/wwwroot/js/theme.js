window.r4eTheme = (() => {
    const storageKey = "r4e-theme";

    const applyPreference = (preference) => {
        const root = document.documentElement;
        if (!preference || preference === "system") {
            root.removeAttribute("data-theme");
            return;
        }

        root.setAttribute("data-theme", preference);
    };

    const getPreference = () => {
        try {
            return localStorage.getItem(storageKey) || "system";
        } catch (error) {
            return "system";
        }
    };

    const setPreference = (preference) => {
        try {
            localStorage.setItem(storageKey, preference);
        } catch (error) {
            return;
        }

        applyPreference(preference);
    };

    const init = () => {
        applyPreference(getPreference());
    };

    return {
        getPreference,
        setPreference,
        init
    };
})();

window.r4eTheme.init();
