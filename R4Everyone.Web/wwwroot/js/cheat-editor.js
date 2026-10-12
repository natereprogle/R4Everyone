window.r4eCheatEditor = (() => {
    const getSelection = (element) => ({
        start: element?.selectionStart ?? 0,
        end: element?.selectionEnd ?? 0
    });

    const setSelection = (element, start, end) => {
        element?.setSelectionRange?.(start, end);
    };

    // Blocks typed characters other than hex digits and spaces. Pasted text is cleaned up by the editor instead.
    const bindHexFilter = (element) => {
        if (!element || element.dataset.hexFilterBound === "true") {
            return;
        }

        element.dataset.hexFilterBound = "true";
        element.addEventListener("keydown", (event) => {
            if (event.defaultPrevented || event.ctrlKey || event.metaKey || event.altKey || event.key.length !== 1) {
                return;
            }

            if (event.key !== " " && !/^[0-9a-fA-F]$/.test(event.key)) {
                event.preventDefault();
            }
        });
    };

    return { getSelection, setSelection, bindHexFilter };
})();
