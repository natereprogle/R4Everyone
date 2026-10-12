window.r4eFiles = (() => {
    const download = (fileName, contentType, bytes) => {
        const blob = new Blob([bytes], { type: contentType });
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
        // Revoking synchronously can cancel the download in some browsers.
        setTimeout(() => URL.revokeObjectURL(url), 0);
    };

    return { download };
})();
