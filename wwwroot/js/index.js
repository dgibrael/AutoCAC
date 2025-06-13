let rpmsTerm = new Terminal({ convertEol: true, fontSize: 14, scrollback:200 });
const rpmsTxtDivId = "rpmsOutputTxtDiv";
let rpmsBufferedText = "";

window.writeRPMSXterm = function (text) {
    rpmsBufferedText += text;
    const container = document.getElementById(rpmsTxtDivId);
    if (!container) {
        return;
    }
    if (!rpmsTerm._core || !rpmsTerm._core._renderService?.dimensions) {
        rpmsTerm.open(container);
    }
    rpmsTerm.write(text);
};

window.clearRPMSXterm = function () {
    rpmsBufferedText = "";
    if (rpmsTerm) {
        rpmsTerm.clear();
    }
};

window.reinitRPMSXterm = function () {
    const container = document.getElementById(rpmsTxtDivId);
    if (!container) {
        console.warn("Target div not found:", rpmsTxtDivId);
        return;
    }

    rpmsTerm = new Terminal({ convertEol: true, fontSize: 14, scrollback: 200 });
    rpmsTerm.open(container);

    if (rpmsBufferedText) {
        rpmsTerm.write(rpmsBufferedText);
    }

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

window.downloadTextFile = function (text, filename, mimeType = "text/plain") {
    const blob = new Blob([text], { type: mimeType });
    const link = document.createElement("a");
    link.href = URL.createObjectURL(blob);
    link.download = filename;
    document.body.appendChild(link); // For Firefox compatibility
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(link.href);
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

