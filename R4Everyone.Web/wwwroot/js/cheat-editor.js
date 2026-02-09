window.r4eCheatEditor = window.r4eCheatEditor || {};

window.r4eCheatEditor.getState = function (element) {
    if (!element) {
        return { start: 0, end: 0 };
    }

    return {
        start: element.selectionStart || 0,
        end: element.selectionEnd || 0
    };
};

window.r4eCheatEditor.setSelection = function (element, start, end) {
    if (!element || typeof element.setSelectionRange !== "function") {
        return;
    }

    element.setSelectionRange(start, end);
};

window.r4eCheatEditor.bindHexFilter = function (element) {
    if (!element || element.dataset.hexFilterBound === "true") {
        return;
    }

    element.dataset.hexFilterBound = "true";
    element.addEventListener("keydown", function (event) {
        if (event.defaultPrevented || event.ctrlKey || event.metaKey || event.altKey) {
            return;
        }

        let key = event.key;
        if (key.length !== 1) {
            return;
        }

        if (key === " ") {
            return;
        }

        if (!/^[0-9a-fA-F]$/.test(key)) {
            event.preventDefault();
        }
    });
};
