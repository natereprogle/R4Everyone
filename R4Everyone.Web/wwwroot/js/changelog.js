window.r4eChangelog = (() => {
    const storageKey = "r4e-changelog-version";

    const getSeenVersion = () => {
        try {
            return localStorage.getItem(storageKey) || "";
        } catch (error) {
            console.warn("R4Everyone: could not read the last seen changelog version.", error);
            return "";
        }
    };

    const setSeenVersion = (version) => {
        try {
            localStorage.setItem(storageKey, version || "");
        } catch (error) {
            console.warn("R4Everyone: could not save the last seen changelog version.", error);
        }
    };

    return { getSeenVersion, setSeenVersion };
})();
