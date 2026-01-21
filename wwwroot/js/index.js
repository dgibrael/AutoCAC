let rpmsTerm = new Terminal({ convertEol: true, fontSize: 14, scrollback: 200 });
const rpmsTxtDivId = "rpmsOutputTxtDiv";

let rpmsDotNetRef = null;

window.setRPMSDotNetRef = function (dotNetRef) {
    rpmsDotNetRef = dotNetRef;
};

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
        if (!rpmsDotNetRef) {
            console.warn("RPMS .NET reference not set.");
            return;
        }
        rpmsDotNetRef.invokeMethodAsync("UserInput", input);
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

window.blazorPrintDialog = {
    openAndPrint() {
        // Cleanup any leftovers
        document.getElementById("print-dialog")?.remove();
        document.getElementById("blazor-print-style")?.remove();

        // Create dialog
        const dialog = document.createElement("dialog");
        dialog.id = "print-dialog";
        dialog.style.padding = "0";
        dialog.style.width = "100%";
        dialog.style.maxWidth = "100%";

        const container = document.createElement("div");
        container.className = "print-dialog-content";

        // Clone print content
        document.querySelectorAll(".print-include").forEach(el => {
            const src = el.querySelector(".print-source"); // see Razor note below
            if (!src) return;

            const clone = src.cloneNode(true);

            // Ensure it renders even if it was screen-hidden
            clone.classList.remove("screen-hidden");
            clone.removeAttribute("hidden");
            if (clone.style) clone.style.display = "";

            container.appendChild(clone);
        });

        dialog.appendChild(container);
        document.body.appendChild(dialog);

        // Inject print-only CSS scoped to this dialog
        const style = document.createElement("style");
        style.id = "blazor-print-style";
        style.textContent = `
        @media print {
          /* Only print the dialog */
          body > *:not(#${dialog.id}) { display: none !important; }

          dialog#${dialog.id} {
            display: block !important;
            position: static !important;
            border: none !important;
            margin: 0 !important;
            padding: 0 !important;
            width: 100% !important;
            max-width: 100% !important;
          }

          dialog#${dialog.id}::backdrop { display: none !important; }

          /* ✅ Key part: remove scroll containers so content can paginate */
          dialog#${dialog.id},
          dialog#${dialog.id} * {
            overflow: visible !important;
            max-height: none !important;
            height: auto !important;
          }

          /* Tables: repeat header on each page (works in most browsers) */
          thead { display: table-header-group; }
          tfoot { display: table-footer-group; }

          /* Long code/text blocks should wrap instead of overflowing */
          pre, code {
            white-space: pre-wrap !important;
            word-break: break-word !important;
          }

          /* If you have sticky headers/positioned stuff, neutralize for print */
          .sticky, [style*="position: sticky"] {
            position: static !important;
          }
        }
        `;

        document.head.appendChild(style);

        const cleanup = () => {
            window.removeEventListener("afterprint", cleanup);
            try { dialog.close(); } catch { }
            dialog.remove();
            style.remove();
        };

        window.addEventListener("afterprint", cleanup, { once: true });

        dialog.showModal();

        // Ensure layout/paint before print snapshot
        requestAnimationFrame(() => {
            requestAnimationFrame(() => window.print());
        });
    }
};

window.GetChatDraftText = function (elementId) {
    const root = document.getElementById(elementId);
    if (!root) return "";

    // Most likely a <textarea>, but this works for input too.
    const el = root.querySelector(".rz-chat-textarea");
    if (!el) return "";

    // For textarea/input, the current text is in .value (not textContent)
    return el.value || "";
};