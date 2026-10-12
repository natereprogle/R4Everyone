// Drag-and-drop reordering built on pointer events, so it works the same with a mouse, touch, or pen.
//
// Markup contract, inside an attached container:
//   [data-drag-id]      an item that can be dragged; holds a [data-drag-handle] button to start the drag
//   [data-drop-id]      an item that can be dropped on
//   data-drag-kind      "game", "folder", or "cheat"
//   data-drag-parent    id of the list the item belongs to (folders only reorder within their own list)
//   data-level          tree depth, used to draw the "after" line below an expanded folder's children
//   data-has-children   "true" when a folder shows children below it
//
// Drops are reported to .NET as OnDrop(sourceId, targetId, "before" | "after" | "inside"), and arrow keys on a
// focused handle as OnKeyboardMove(id, -1 | 1).
window.r4eDragDrop = (() => {
    const dragThreshold = 4;
    const autoScrollEdge = 48;
    const autoScrollMaxSpeed = 14;
    const controllers = new Map();

    const report = (promise, action) =>
        promise.catch((error) => console.error(`R4Everyone: failed to ${action}.`, error));

    const findScrollParent = (element) => {
        for (let node = element; node && node !== document.body; node = node.parentElement) {
            const overflowY = getComputedStyle(node).overflowY;
            if ((overflowY === "auto" || overflowY === "scroll") && node.scrollHeight > node.clientHeight) {
                return node;
            }
        }

        return document.scrollingElement;
    };

    // The row an "after" line is drawn under: an expanded folder's last visible descendant.
    const lastVisibleRow = (row) => {
        if (row.dataset.hasChildren !== "true") {
            return row;
        }

        const level = Number(row.dataset.level);
        let last = row;
        for (let next = row.nextElementSibling; next?.dataset.dropId; next = next.nextElementSibling) {
            if (Number(next.dataset.level) <= level) {
                break;
            }

            last = next;
        }

        return last;
    };

    const dropPosition = (state, drag, target, x, y) => {
        const rect = target.getBoundingClientRect();
        if (state.axis === "x") {
            return x < rect.left + rect.width / 2 ? "before" : "after";
        }

        const ratio = (y - rect.top) / rect.height;
        if (drag.kind === "folder") {
            return target.dataset.dragParent === drag.parent ? (ratio < 0.5 ? "before" : "after") : null;
        }

        if (drag.kind === "cheat" && target.dataset.dragKind === "folder") {
            if (ratio < 0.25) {
                return "before";
            }

            // Dropping below an expanded folder means "inside" it; "after" is only offered when it is collapsed.
            return ratio > 0.75 && target.dataset.hasChildren !== "true" ? "after" : "inside";
        }

        return ratio < 0.5 ? "before" : "after";
    };

    const clearIndicator = (drag) => {
        drag.indicator?.classList.remove("drop-before", "drop-after", "drop-inside");
        drag.indicator = null;
        drag.target = null;
        drag.position = null;
    };

    const updateTarget = (state, drag) => {
        const element = document.elementFromPoint(drag.x, drag.y)?.closest("[data-drop-id]");
        const valid = element && state.container.contains(element) && element !== drag.item;
        const position = valid ? dropPosition(state, drag, element, drag.x, drag.y) : null;
        if (!position) {
            clearIndicator(drag);
            return;
        }

        const indicator = position === "after" && state.axis === "y" ? lastVisibleRow(element) : element;
        if (indicator !== drag.indicator || position !== drag.position) {
            clearIndicator(drag);
            indicator.classList.add(`drop-${position}`);
        }

        drag.indicator = indicator;
        drag.target = element;
        drag.position = position;
    };

    const autoScroll = (state) => {
        const drag = state.drag;
        if (!drag?.active) {
            return;
        }

        const scroller = drag.scroller;
        const bounds = scroller === document.scrollingElement
            ? { top: 0, bottom: window.innerHeight }
            : scroller.getBoundingClientRect();

        let speed = 0;
        if (drag.y < bounds.top + autoScrollEdge) {
            speed = -autoScrollMaxSpeed * (1 - Math.max(0, drag.y - bounds.top) / autoScrollEdge);
        } else if (drag.y > bounds.bottom - autoScrollEdge) {
            speed = autoScrollMaxSpeed * (1 - Math.max(0, bounds.bottom - drag.y) / autoScrollEdge);
        }

        if (speed !== 0) {
            scroller.scrollTop += speed;
            updateTarget(state, drag);
        }

        drag.frame = requestAnimationFrame(() => autoScroll(state));
    };

    const startDrag = (state, drag) => {
        drag.active = true;
        const rect = drag.item.getBoundingClientRect();
        drag.offsetX = drag.startX - rect.left;
        drag.offsetY = drag.startY - rect.top;

        const ghost = drag.item.cloneNode(true);
        ghost.removeAttribute("data-drag-id");
        ghost.removeAttribute("data-drop-id");
        ghost.classList.add("drag-ghost");
        ghost.setAttribute("aria-hidden", "true");
        ghost.style.width = `${rect.width}px`;
        document.body.appendChild(ghost);
        drag.ghost = ghost;

        drag.item.classList.add("is-dragging");
        document.body.classList.add("is-drag-active");
        drag.frame = requestAnimationFrame(() => autoScroll(state));
    };

    const moveGhost = (drag) => {
        drag.ghost.style.transform = `translate(${drag.x - drag.offsetX}px, ${drag.y - drag.offsetY}px)`;
    };

    const endDrag = (state, drop) => {
        const drag = state.drag;
        if (!drag) {
            return;
        }

        state.drag = null;
        drag.handle.removeEventListener("pointermove", drag.onMove);
        drag.handle.removeEventListener("pointerup", drag.onUp);
        drag.handle.removeEventListener("pointercancel", drag.onCancel);
        window.removeEventListener("keydown", drag.onKey, true);
        cancelAnimationFrame(drag.frame);

        const targetId = drop && drag.active && drag.target ? Number(drag.target.dataset.dropId) : null;
        const position = drag.position;
        clearIndicator(drag);
        drag.ghost?.remove();
        drag.item.classList.remove("is-dragging");
        document.body.classList.remove("is-drag-active");

        if (targetId !== null) {
            report(state.dotNetRef.invokeMethodAsync("OnDrop", Number(drag.id), targetId, position), "move the item");
        }
    };

    const onPointerDown = (state, event) => {
        const handle = event.target.closest("[data-drag-handle]");
        const item = handle?.closest("[data-drag-id]");
        if (!item || !state.container.contains(item) || event.button !== 0 || state.drag) {
            return;
        }

        event.preventDefault();
        const drag = {
            handle,
            item,
            id: item.dataset.dragId,
            kind: item.dataset.dragKind,
            parent: item.dataset.dragParent,
            startX: event.clientX,
            startY: event.clientY,
            x: event.clientX,
            y: event.clientY,
            active: false,
            scroller: findScrollParent(state.container)
        };

        drag.onMove = (moveEvent) => {
            drag.x = moveEvent.clientX;
            drag.y = moveEvent.clientY;
            if (!drag.active) {
                if (Math.hypot(drag.x - drag.startX, drag.y - drag.startY) < dragThreshold) {
                    return;
                }

                startDrag(state, drag);
            }

            moveGhost(drag);
            updateTarget(state, drag);
        };
        drag.onUp = () => endDrag(state, true);
        drag.onCancel = () => endDrag(state, false);
        drag.onKey = (keyEvent) => {
            if (keyEvent.key === "Escape") {
                keyEvent.preventDefault();
                endDrag(state, false);
            }
        };

        handle.setPointerCapture(event.pointerId);
        handle.addEventListener("pointermove", drag.onMove);
        handle.addEventListener("pointerup", drag.onUp);
        handle.addEventListener("pointercancel", drag.onCancel);
        window.addEventListener("keydown", drag.onKey, true);
        state.drag = drag;
    };

    const onKeyDown = (state, event) => {
        const handle = event.target.closest?.("[data-drag-handle]");
        const item = handle?.closest("[data-drag-id]");
        const offset = { ArrowUp: -1, ArrowLeft: -1, ArrowDown: 1, ArrowRight: 1 }[event.key];
        if (!item || !offset || state.drag || event.altKey || event.ctrlKey || event.metaKey) {
            return;
        }

        event.preventDefault();
        const id = item.dataset.dragId;
        report(
            state.dotNetRef.invokeMethodAsync("OnKeyboardMove", Number(id), offset)
                .then(() => requestAnimationFrame(() =>
                    state.container.querySelector(`[data-drag-id="${id}"] [data-drag-handle]`)?.focus())),
            "move the item");
    };

    // A Blazor component's element may already be gone when it is disposed, so also drop any controller whose
    // container has left the page.
    const forgetDisconnected = () => {
        for (const container of [...controllers.keys()]) {
            if (!container.isConnected) {
                detach(container);
            }
        }
    };

    const attach = (container, dotNetRef, options) => {
        forgetDisconnected();
        detach(container);
        const state = { container, dotNetRef, axis: options?.axis === "x" ? "x" : "y", drag: null };
        state.onPointerDown = (event) => onPointerDown(state, event);
        state.onKeyDown = (event) => onKeyDown(state, event);
        container.addEventListener("pointerdown", state.onPointerDown);
        container.addEventListener("keydown", state.onKeyDown);
        controllers.set(container, state);
    };

    const detach = (container) => {
        const state = container && controllers.get(container);
        if (!state) {
            forgetDisconnected();
            return;
        }

        endDrag(state, false);
        container.removeEventListener("pointerdown", state.onPointerDown);
        container.removeEventListener("keydown", state.onKeyDown);
        controllers.delete(container);
    };

    return { attach, detach };
})();
