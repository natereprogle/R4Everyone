window.r4eChangelog = window.r4eChangelog || {};

window.r4eChangelog.getSeenVersion = function () {
    try {
        return localStorage.getItem("r4e-changelog-version") || "";
    } catch (error) {
        return "";
    }
};

window.r4eChangelog.setSeenVersion = function (version) {
    try {
        localStorage.setItem("r4e-changelog-version", version || "");
    } catch (error) {
        // Ignore storage errors.
    }
};
