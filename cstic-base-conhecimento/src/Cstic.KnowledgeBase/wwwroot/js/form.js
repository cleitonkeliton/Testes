"use strict";

(() => {
    const form = document.getElementById("article-form");
    const actionsList = document.getElementById("actions-list");
    const message = document.getElementById("form-message");
    const generateButton = document.getElementById("generate-document");
    const reviewMode = document.body.dataset.mode === "validar";
    const editorLayout = document.getElementById("editor-layout");
    const importButton = document.getElementById("import-document");
    const fileInput = document.getElementById("existing-file");
    const importMessage = document.getElementById("import-message");
    const generateLabel = reviewMode ? "Gerar base revisada" : "Gerar documento Word";
    let sequence = 0;
    let edited = false;
    let busy = false;
    const actions = () => [...actionsList.querySelectorAll(":scope > .procedure-action")];
    const steps = action => [...(action || actionsList).querySelectorAll(action ? ":scope > .action-steps > .procedure-step" : ".procedure-step")];

    function notify(text, error = false) {
        message.replaceChildren(document.createTextNode(text));
        message.className = error ? "notice error" : "notice";
        message.setAttribute("role", error ? "alert" : "status");
        message.hidden = false;
        message.focus();
    }

    function clearErrors() {
        form.querySelectorAll(".field-error").forEach(node => node.remove());
        form.querySelectorAll('[aria-invalid="true"]').forEach(node => {
            node.removeAttribute("aria-invalid");
            const errorId = node.dataset.errorId;
            if (errorId) {
                const describedBy = (node.getAttribute("aria-describedby") || "")
                    .split(" ").filter(id => id && id !== errorId).join(" ");
                if (describedBy) node.setAttribute("aria-describedby", describedBy);
                else node.removeAttribute("aria-describedby");
                delete node.dataset.errorId;
            }
        });
        message.hidden = true;
    }

    function addDetail(step, value = "") {
        const list = step.querySelector(".detail-list");
        if (list.children.length >= 20) return;
        const node = document.getElementById("detail-template").content.firstElementChild.cloneNode(true);
        const input = node.querySelector("textarea");
        input.id = `detail-${++sequence}`;
        input.value = value;
        node.querySelector("label").htmlFor = input.id;
        list.append(node);
        return input;
    }

    function addStep(action, instruction = "", details = []) {
        if (steps().length >= 100) return;
        const node = document.getElementById("step-template").content.firstElementChild.cloneNode(true);
        const input = node.querySelector(".step-instruction");
        input.id = `instruction-${++sequence}`;
        input.value = instruction;
        node.querySelector(".instruction-label").htmlFor = input.id;
        action.querySelector(".action-steps").append(node);
        details.forEach(detail => addDetail(node, detail));
        return input;
    }

    function addAction(title = "", initialSteps = [{ instruction: "", details: [] }]) {
        if (actions().length >= 20) return;
        const node = document.getElementById("action-template").content.firstElementChild.cloneNode(true);
        const input = node.querySelector(".action-title");
        input.id = `action-${++sequence}`;
        input.value = title;
        node.querySelector(".action-title-label").htmlFor = input.id;
        actionsList.append(node);
        initialSteps.forEach(step => addStep(node, step.instruction, step.details || []));
        return input;
    }

    function update() {
        const current = actions();
        const total = steps().length;
        current.forEach((action, actionIndex) => {
            const number = `${actionIndex + 1}º`;
            action.querySelector(".action-number").textContent = number;
            action.querySelector(".action-title").dataset.path = `actions[${actionIndex}].title`;
            const controls = action.querySelector(".action-heading .step-controls");
            controls.querySelector('[data-action="action-up"]').disabled = actionIndex === 0;
            controls.querySelector('[data-action="action-down"]').disabled = actionIndex === current.length - 1;
            controls.querySelector('[data-action="remove-action"]').disabled = current.length === 1;
            controls.querySelector('[data-action="action-up"]').setAttribute("aria-label", `Mover ação ${number} para cima`);
            controls.querySelector('[data-action="action-down"]').setAttribute("aria-label", `Mover ação ${number} para baixo`);
            controls.querySelector('[data-action="remove-action"]').setAttribute("aria-label", `Remover ação ${number}`);
            action.querySelector('[data-action="add-substep"]').disabled = total >= 100;
            const children = steps(action);
            children.forEach((step, index) => {
                const stepNumber = `1.${index + 1}`;
                const prefix = `actions[${actionIndex}].steps[${index}]`;
                step.querySelector(".step-number").textContent = stepNumber;
                step.querySelector(".step-instruction").dataset.path = `${prefix}.instruction`;
                step.querySelectorAll(".detail-text").forEach((input, detailIndex) => {
                    input.dataset.path = `${prefix}.details[${detailIndex}]`;
                });
                const buttons = step.querySelector(".step-controls");
                buttons.querySelector('[data-action="up"]').disabled = index === 0;
                buttons.querySelector('[data-action="down"]').disabled = index === children.length - 1;
                buttons.querySelector('[data-action="remove-step"]').disabled = children.length === 1;
                buttons.querySelector('[data-action="up"]').setAttribute("aria-label", `Mover subetapa ${stepNumber} da ação ${number} para cima`);
                buttons.querySelector('[data-action="down"]').setAttribute("aria-label", `Mover subetapa ${stepNumber} da ação ${number} para baixo`);
                buttons.querySelector('[data-action="remove-step"]').setAttribute("aria-label", `Remover subetapa ${stepNumber} da ação ${number}`);
                step.querySelector('[data-action="add-detail"]').disabled = step.querySelectorAll(".detail-row").length >= 20;
                step.querySelector('[data-action="add-detail"]').setAttribute("aria-label", `Adicionar detalhe à subetapa ${stepNumber} da ação ${number}`);
            });
        });
        document.getElementById("add-action").disabled = current.length >= 20 || total >= 100;
        document.getElementById("step-counter").textContent = `${current.length} ${current.length === 1 ? "ação" : "ações"}`;
        document.getElementById("summary-steps").textContent = `${String(current.length).padStart(2, "0")} / ${String(total).padStart(2, "0")}`;
        const title = document.getElementById("title").value.trim();
        const code = document.getElementById("code").value.trim();
        const executor = document.getElementById("executor").value;
        document.getElementById("summary-title").textContent = title || "Novo procedimento";
        document.getElementById("summary-code").textContent = code ? `POP Nº ${code} · Versão ${document.getElementById("version").value || "—"}` : "O título e o número aparecem aqui.";
        document.getElementById("summary-executor").textContent = executor;
        document.getElementById("executor-summary").textContent = `${executor}, conforme selecionado na identificação.`;
        const required = [...form.querySelectorAll("[required]")];
        const complete = required.filter(input => input.value.trim() && input.validity.valid).length;
        const percentage = Math.round(complete / required.length * 100);
        document.getElementById("form-progress").value = percentage;
        document.getElementById("progress-percentage").textContent = `${percentage}%`;
        document.getElementById("progress-description").textContent = `${complete} de ${required.length} campos obrigatórios preenchidos.`;
    }

    function payload() {
        const result = {};
        form.querySelectorAll("[name][data-path]").forEach(input => result[input.name] = input.value.trim());
        result.actions = actions().map(action => ({
            title: action.querySelector(".action-title").value.trim(),
            steps: steps(action).map(step => ({
                instruction: step.querySelector(".step-instruction").value.trim(),
                details: [...step.querySelectorAll(".detail-text")].map(input => input.value.trim())
            }))
        }));
        return result;
    }

    function showValidation(response) {
        notify(response.message || "Revise os campos e tente novamente.", true);
        if (!response.errors) return;
        const list = document.createElement("ul");
        let firstInput = null;
        for (const [path, errors] of Object.entries(response.errors)) {
            const input = [...form.querySelectorAll("[data-path]")].find(node => node.dataset.path === path);
            for (const text of errors) {
                const item = document.createElement("li");
                if (input) {
                    const link = document.createElement("a");
                    link.href = `#${input.id}`;
                    link.textContent = text;
                    link.addEventListener("click", () => input.focus());
                    item.append(link);
                } else item.textContent = text;
                list.append(item);
            }
            if (input) {
                firstInput ||= input;
                const error = document.createElement("span");
                error.className = "field-error";
                error.id = `error-${++sequence}`;
                error.textContent = errors.join(" ");
                input.after(error);
                input.dataset.errorId = error.id;
                input.setAttribute("aria-invalid", "true");
                input.setAttribute("aria-describedby", `${input.getAttribute("aria-describedby") || ""} ${error.id}`.trim());
            }
        }
        message.append(list);
        if (firstInput) firstInput.focus();
    }

    form.addEventListener("input", () => {
        edited = true;
        clearErrors();
        update();
    });
    document.getElementById("add-action").addEventListener("click", () => {
        edited = true;
        clearErrors();
        const input = addAction();
        update();
        input?.focus();
    });
    actionsList.addEventListener("click", event => {
        const button = event.target.closest("button[data-action]");
        if (!button || button.disabled) return;
        const step = button.closest(".procedure-step");
        const action = button.closest(".procedure-action");
        edited = true;
        clearErrors();
        switch (button.dataset.action) {
            case "action-up":
                if (action.previousElementSibling) actionsList.insertBefore(action, action.previousElementSibling);
                button.focus();
                break;
            case "action-down":
                if (action.nextElementSibling) actionsList.insertBefore(action.nextElementSibling, action);
                button.focus();
                break;
            case "remove-action": {
                const next = action.nextElementSibling || action.previousElementSibling;
                if (actions().length > 1) action.remove();
                next?.querySelector(".action-title").focus();
                break;
            }
            case "add-substep":
                addStep(action)?.focus();
                break;
            case "up":
                if (step.previousElementSibling) step.parentElement.insertBefore(step, step.previousElementSibling);
                button.focus();
                break;
            case "down":
                if (step.nextElementSibling) step.parentElement.insertBefore(step.nextElementSibling, step);
                button.focus();
                break;
            case "remove-step": {
                const next = step.nextElementSibling || step.previousElementSibling;
                if (steps(action).length > 1) step.remove();
                next?.querySelector("textarea").focus();
                break;
            }
            case "add-detail":
                addDetail(step)?.focus();
                break;
            case "remove-detail":
                button.closest(".detail-row").remove();
                step.querySelector('[data-action="add-detail"]').focus();
                break;
        }
        update();
    });

    document.getElementById("clear-form").addEventListener("click", () => {
        if (busy) return;
        if (!window.confirm("Limpar todos os campos? Os dados preenchidos serão descartados.")) return;
        form.reset();
        actionsList.replaceChildren();
        addAction();
        clearErrors();
        edited = false;
        update();
        if (reviewMode) {
            editorLayout.hidden = true;
            importMessage.hidden = true;
            fileInput.value = "";
            fileInput.focus();
        } else document.getElementById("code").focus();
    });

    function importNotice(text, error = false) {
        importMessage.textContent = text;
        importMessage.className = error ? "notice error" : "notice";
        importMessage.setAttribute("role", error ? "alert" : "status");
        importMessage.hidden = false;
        importMessage.focus();
    }

    importButton?.addEventListener("click", async () => {
        if (busy) return;
        const file = fileInput.files[0];
        if (!file) { importNotice("Selecione o arquivo Word que deseja validar.", true); return; }
        if (!file.name.toLowerCase().endsWith(".docx")) {
            importNotice("Envie um arquivo .docx no modelo CSTIC. PDF e .doc não são aceitos.", true); return;
        }
        if (file.size > 2 * 1024 * 1024) { importNotice("O arquivo deve ter no máximo 2 MB.", true); return; }
        if (edited && !window.confirm("Substituir a revisão atual pelo documento selecionado? As alterações não baixadas serão descartadas.")) return;
        const data = new FormData();
        data.set("UploadedFile", file);
        data.set("__RequestVerificationToken", form.querySelector('[name="__RequestVerificationToken"]').value);
        busy = true;
        importButton.disabled = true;
        generateButton.disabled = true;
        importButton.textContent = "Lendo documento…";
        try {
            const response = await fetch("/validar?handler=Import", { method: "POST", body: data, credentials: "same-origin" });
            if (!response.ok) {
                const info = response.headers.get("content-type")?.includes("application/json") ? await response.json() : null;
                importNotice(info?.message || (response.status === 413 ? "O arquivo deve ter no máximo 2 MB." : "Não foi possível importar. Recarregue a página se a sessão expirou e tente novamente."), true);
                return;
            }
            const imported = await response.json();
            const article = imported.article;
            const executor = document.getElementById("executor");
            executor.querySelectorAll("[data-imported]").forEach(option => option.remove());
            if (![...executor.options].some(option => option.value === article.executor)) {
                const option = new Option(`${article.executor || "Não informado"} — selecione N1 ou N2`, article.executor);
                option.dataset.imported = "true";
                executor.add(option);
            }
            form.querySelectorAll("[name][data-path]").forEach(input => input.value = article[input.name] ?? "");
            actionsList.replaceChildren();
            article.actions.forEach(action => addAction(action.title, action.steps));
            clearErrors();
            editorLayout.hidden = false;
            edited = false;
            update();
            const date = imported.originalDate ? ` Data no arquivo: ${imported.originalDate}.` : "";
            importNotice(`Arquivo carregado: ${imported.fileName}.${date} Confira todos os campos, incluindo autor, aprovador e versão. ${imported.warnings.join(" ")}`);
            document.getElementById("identificacao").scrollIntoView({ behavior: "smooth", block: "start" });
            document.getElementById("title").focus({ preventScroll: true });
        } catch {
            importNotice("Não foi possível ler o arquivo. Confira a conexão e tente novamente; a revisão atual continua preenchida.", true);
        } finally {
            busy = false;
            importButton.disabled = false;
            generateButton.disabled = false;
            importButton.textContent = "Carregar para revisão";
        }
    });

    document.getElementById("fill-example").addEventListener("click", () => {
        if (edited && !window.confirm("Substituir os dados preenchidos pelo exemplo?")) return;
        const example = {
            code: "0001",
            title: "Manutenção preventiva de computador administrativo",
            version: "1.0",
            author: "Técnico de exemplo",
            approver: "Marcone Jose Roque Luna",
            executor: "N2 / N1",
            objective: "Padronizar a manutenção preventiva de um computador de uso administrativo.",
            result: "Computador montado, com funcionamento verificado e eventuais defeitos registrados.",
            requirements: "Ferramentas e materiais autorizados pela instituição; instruções do fabricante.\nComputador administrativo desligado e desconectado da alimentação."
        };
        for (const [field, value] of Object.entries(example)) document.getElementById(field).value = value;
        actionsList.replaceChildren();
        addAction("Abrir computador", [
            { instruction: "Retirar os parafusos.", details: ["Identificar e guardar os parafusos para a montagem."] },
            { instruction: "Retirar a tampa." }
        ]);
        addAction("Limpeza", [
            { instruction: "Retirar os componentes necessários à limpeza." },
            { instruction: "Realizar a limpeza conforme as instruções do fabricante.", details: ["Utilizar materiais autorizados pela instituição."] }
        ]);
        addAction("Preventivas", [
            { instruction: "Verificar a necessidade de manutenção da pasta térmica." },
            { instruction: "Verificar o encaixe dos conectores." },
            { instruction: "Identificar itens defeituosos." }
        ]);
        addAction("Montagem", [
            { instruction: "Recolocar todas as peças." },
            { instruction: "Fechar o gabinete." },
            { instruction: "Recolocar os parafusos." },
            { instruction: "Testar o funcionamento." }
        ]);
        clearErrors();
        edited = true;
        update();
        notify("Exemplo preenchido. Revise os dados antes de gerar seu documento.");
    });

    form.addEventListener("submit", async event => {
        event.preventDefault();
        if (busy) return;
        clearErrors();
        if (!form.reportValidity()) return;
        const snapshot = payload();
        const data = new FormData();
        data.set("Payload", JSON.stringify(snapshot));
        data.set("__RequestVerificationToken", form.querySelector('[name="__RequestVerificationToken"]').value);
        busy = true;
        generateButton.disabled = true;
        if (importButton) importButton.disabled = true;
        generateButton.querySelector("span").textContent = "Gerando documento…";
        try {
            const response = await fetch(form.action, { method: "POST", body: data, credentials: "same-origin" });
            if (!response.ok) {
                if (response.headers.get("content-type")?.includes("application/json")) showValidation(await response.json());
                else if (response.status === 400) notify("A sessão do formulário expirou. Recarregue a página e tente novamente.", true);
                else if (response.status === 413) notify("O conteúdo ficou muito extenso. Reduza o texto e tente novamente.", true);
                else notify("Não foi possível gerar o documento. Tente novamente em instantes.", true);
                return;
            }
            const blob = await response.blob();
            if (!blob.size) throw new Error("Empty document");
            const url = URL.createObjectURL(blob);
            const link = document.createElement("a");
            link.href = url;
            const disposition = response.headers.get("content-disposition") || "";
            const encoded = disposition.match(/filename\*=UTF-8''([^;]+)/i);
            const plain = disposition.match(/filename="?([^";]+)"?/i);
            let filename = "POP_CSTIC.docx";
            if (encoded) filename = decodeURIComponent(encoded[1]);
            else if (plain) filename = plain[1].trim();
            link.download = filename;
            document.body.append(link);
            link.click();
            link.remove();
            window.setTimeout(() => URL.revokeObjectURL(url), 30_000);
            notify("Documento Word gerado. O download foi solicitado ao navegador. Para PDF, abra o arquivo no Word ou LibreOffice e escolha Exportar como PDF.");
            if (JSON.stringify(payload()) === JSON.stringify(snapshot)) edited = false;
        } catch {
            notify("Não foi possível concluir o download. Confira sua conexão e tente novamente; seus campos continuam preenchidos.", true);
        } finally {
            busy = false;
            generateButton.disabled = false;
            if (importButton) importButton.disabled = false;
            generateButton.querySelector("span").textContent = generateLabel;
        }
    });

    addAction();
    update();
})();
