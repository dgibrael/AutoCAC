let rpmsTerm = new Terminal({ convertEol: true, fontSize: 14 });
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

window.getRPMSXtermContent = function (maxLines = null, asString = true) {
    if (!rpmsTerm) {
        console.warn("Terminal is not initialized.");
        return asString ? "" : [];
    }

    const buffer = rpmsTerm.buffer.active;
    const totalLines = buffer.length;
    const lines = [];

    const startLine = maxLines && maxLines < totalLines
        ? totalLines - maxLines
        : 0;

    for (let i = startLine; i < totalLines; i++) {
        const line = buffer.getLine(i)?.translateToString(true) ?? "";
        lines.push(line);
    }

    return asString ? lines.join("\n") : lines;
};

function trimRPMSXterm(n = 1000) {
    const lines = getRPMSXtermContent(n, false);

    rpmsTerm.clear();
    for (const line of lines) {
        rpmsTerm.writeln(line);
    }
}

window.downloadRPMSContent = function () {
    let content = getRPMSXtermContent();
    if (content != null) {
        downloadTextFile(content, "rpms_output.txt", "text/plain")
    }
}

window.runCSharp = function (methodName, data) {
    if (data === undefined) {
        return DotNet.invokeMethodAsync("AutoCAC", methodName);
    } else {
        return DotNet.invokeMethodAsync("AutoCAC", methodName, data);
    }
};
