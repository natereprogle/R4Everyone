window.r4eShortcuts = (() => {
    // Text fields keep the browser's own undo/redo for the text being typed.
    const nonTextInputTypes = ["checkbox", "radio", "button", "submit", "reset", "file", "range", "color"];

    let onKeyDown;

    const isTextEntry = (element) =>
        element instanceof HTMLElement &&
        (element.isContentEditable ||
            element.tagName === "TEXTAREA" ||
            (element.tagName === "INPUT" && !nonTextInputTypes.includes(element.type)));

    const shortcutFor = (event) => {
        if (!(event.ctrlKey || event.metaKey) || event.altKey) {
            return null;
        }

        const key = event.key.toLowerCase();
        if (key === "z") {
            return event.shiftKey ? "redo" : "undo";
        }

        return key === "y" && !event.shiftKey ? "redo" : null;
    };

    const register = (dotNetRef) => {
        unregister();
        onKeyDown = (event) => {
            const shortcut = shortcutFor(event);
            if (!shortcut || isTextEntry(event.target)) {
                return;
            }

            event.preventDefault();
            dotNetRef.invokeMethodAsync("OnShortcut", shortcut)
                .catch((error) => console.error(`R4Everyone: failed to run the ${shortcut} shortcut.`, error));
        };

        document.addEventListener("keydown", onKeyDown);
    };

    const unregister = () => {
        if (onKeyDown) {
            document.removeEventListener("keydown", onKeyDown);
            onKeyDown = undefined;
        }
    };

    return { register, unregister };
})();
