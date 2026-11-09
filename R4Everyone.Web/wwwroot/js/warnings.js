window.r4eWarnings = (() => {
    const unofficialFileKey = "r4e-skip-unofficial-file-warning";

    const isUnofficialFileWarningDisabled = () => {
        try {
            return localStorage.getItem(unofficialFileKey) === "true";
        } catch (error) {
            console.warn("R4Everyone: could not read the unofficial file warning setting.", error);
            return false;
        }
    };

    const disableUnofficialFileWarning = () => {
        try {
            localStorage.setItem(unofficialFileKey, "true");
        } catch (error) {
            console.warn("R4Everyone: could not save the unofficial file warning setting.", error);
        }
    };

    return { isUnofficialFileWarningDisabled, disableUnofficialFileWarning };
})();
