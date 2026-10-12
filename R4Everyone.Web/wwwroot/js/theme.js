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
            console.warn("R4Everyone: could not read the theme preference.", error);
            return "system";
        }
    };

    // The theme is applied even when it cannot be saved, so the toggle still works for this session.
    const setPreference = (preference) => {
        applyPreference(preference);
        try {
            localStorage.setItem(storageKey, preference);
        } catch (error) {
            console.warn("R4Everyone: could not save the theme preference.", error);
        }
    };

    return { getPreference, setPreference };
})();
