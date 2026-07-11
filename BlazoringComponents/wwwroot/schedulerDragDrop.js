export function initialize(schedulerElement) {
    if (!schedulerElement) {
        throw new Error("Scheduler element is required.");
    }

    const findTicket = (event) =>
        event.target instanceof Element
            ? event.target.closest("[data-scheduler-ticket]")
            : null;

    const findDropZone = (event) =>
        event.target instanceof Element
            ? event.target.closest("[data-scheduler-drop-zone]")
            : null;

    const handleDragStart = (event) => {
        const ticket = findTicket(event);
        if (!ticket || !schedulerElement.contains(ticket) || !event.dataTransfer) {
            return;
        }

        event.dataTransfer.effectAllowed = "move";
        event.dataTransfer.setData("text/plain", "scheduler-ticket");
        ticket.classList.add("is-dragging");
    };

    const handleDragOver = (event) => {
        const dropZone = findDropZone(event);
        if (!dropZone || !schedulerElement.contains(dropZone)) {
            return;
        }

        event.preventDefault();
        if (event.dataTransfer) {
            event.dataTransfer.dropEffect = "move";
        }
        dropZone.classList.add("is-drag-over");
    };

    const handleDragLeave = (event) => {
        const dropZone = findDropZone(event);
        if (!dropZone || dropZone.contains(event.relatedTarget)) {
            return;
        }

        dropZone.classList.remove("is-drag-over");
    };

    const clearDragState = () => {
        schedulerElement
            .querySelectorAll(".is-dragging, .is-drag-over")
            .forEach((element) =>
                element.classList.remove("is-dragging", "is-drag-over"));
    };

    const handleDrop = (event) => {
        const dropZone = findDropZone(event);
        if (!dropZone || !schedulerElement.contains(dropZone)) {
            return;
        }

        event.preventDefault();
        clearDragState();
    };

    schedulerElement.addEventListener("dragstart", handleDragStart);
    schedulerElement.addEventListener("dragover", handleDragOver);
    schedulerElement.addEventListener("dragleave", handleDragLeave);
    schedulerElement.addEventListener("drop", handleDrop);
    schedulerElement.addEventListener("dragend", clearDragState);

    return {
        dispose() {
            schedulerElement.removeEventListener("dragstart", handleDragStart);
            schedulerElement.removeEventListener("dragover", handleDragOver);
            schedulerElement.removeEventListener("dragleave", handleDragLeave);
            schedulerElement.removeEventListener("drop", handleDrop);
            schedulerElement.removeEventListener("dragend", clearDragState);
            clearDragState();
        }
    };
}
