let rpmsTerm = new Terminal({ convertEol: true, fontSize: 14, scrollback: 200 });
const rpmsTxtDivId = "rpmsOutputTxtDiv";

window.writeRPMSXterm = function (text) {
    const container = document.getElementById(rpmsTxtDivId);
    if (!container) {
        console.warn("Target div not found:", rpmsTxtDivId);
        return;
    }
    if (!rpmsTerm._core || !rpmsTerm._core._renderService?.dimensions) {
        rpmsTerm.open(container);
    }
    rpmsTerm.write(text);
};

window.clearRPMSXterm = function () {
    if (rpmsTerm) {
        rpmsTerm.clear();
    } else {
        console.warn("Terminal not initialized.");
    }
};

window.reinitRPMSXterm = function () {
    const container = document.getElementById(rpmsTxtDivId);
    if (!container) {
        console.warn("Target div not found:", rpmsTxtDivId);
        return;
    }

    rpmsTerm = new Terminal({ convertEol: true, fontSize: 14 });
    rpmsTerm.open(container);

    rpmsTerm.onData(function (input) {
        runCSharp("UserInput", input);
    });
};


window.scrollToElement = function (elementId) {
    const container = document.getElementById("mainContent");
    const target = document.getElementById(elementId);

    if (container && target) {
        container.scrollTop = target.offsetTop + target.offsetHeight - container.clientHeight;
        target.focus();
    } else {
        console.warn("scrollTo: element or container not found");
    }
};

window.scrollToBottom = function () {
    const container = document.getElementById("mainContent");
    if (container) {
        container.scrollTop = container.scrollHeight;
    } else {
        console.warn("scrollToBottom: container not found");
    }
};

window.scrollToTop = function () {
    const container = document.getElementById("mainContent");
    if (container) {
        container.scrollTop = 0;
    } else {
        console.warn("scrollToTop: container not found");
    }
};

// wwwroot/js/index.js
window.downloadTextFile = function (data, filename = "output.txt", mimeType = "text/plain;charset=utf-8", addBom = false) {
    const parts = [];
    if (addBom) {
        // UTF-8 BOM so Excel on Windows recognizes UTF-8 CSV
        parts.push(new Uint8Array([0xEF, 0xBB, 0xBF]));
    }
    parts.push(data);

    const blob = new Blob(parts, { type: mimeType });
    const url = URL.createObjectURL(blob);

    const a = document.createElement("a");
    a.href = url;
    a.download = filename;
    a.style.display = "none";
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);

    URL.revokeObjectURL(url);
};


window.getRPMSXtermContent = function () {
    if (!rpmsTerm) {
        console.warn("Terminal is not initialized.");
        return "";
    }

    const buffer = rpmsTerm.buffer.active;
    const lines = [];

    for (let i = 0; i < buffer.length; i++) {
        lines.push(buffer.getLine(i)?.translateToString(true) ?? "");
    }

    return lines.join("\n");
};

window.downloadRPMSContent = function () {
    let content = getRPMSXtermContent();
    if (content != null) {
        downloadTextFile(content, "output.txt", "text/plain")
    }
};

window.runCSharp = function (methodName, data) {
    if (data === undefined) {
        return DotNet.invokeMethodAsync("AutoCAC", methodName);
    } else {
        return DotNet.invokeMethodAsync("AutoCAC", methodName, data);
    }
};

window.downloadExcel = function (base64, filename="data.xlsx") {
    const link = document.createElement('a');
    link.download = filename;
    link.href = 'data:application/vnd.openxmlformats-officedocument.spreadsheetml.sheet;base64,' + base64;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

window.showDialog = function (id = "RPMSOutputDiv") {
    const dlg = document.getElementById(id);
    if (!dlg) return;
    if (dlg.open) return;

    // Define a named handler so we can remove it later
    function handleOutsideClick(e) {
        if (e.target === dlg) {
            dlg.close();
        }
    }

    // Ensure no duplicate listeners
    dlg.removeEventListener('mousedown', handleOutsideClick);
    dlg.addEventListener('mousedown', handleOutsideClick);
    dlg.showModal();
};


window.hideDialog = function (id = "RPMSOutputDiv") {
    document.getElementById(id).close();
};


window.downloadFileFromStream = async function (fileName, contentStreamReference, type = "text/csv;charset=utf-8") {
    const arrayBuffer = await contentStreamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer], { type });
    const url = URL.createObjectURL(blob);

    const a = document.createElement("a");
    a.href = url;
    a.download = fileName;
    a.style.display = "none"; // hide
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);

    URL.revokeObjectURL(url);
};

window.getWindowHeight = () => window.innerHeight;