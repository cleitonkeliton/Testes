"use strict";

(() => {
    // Lets the server answer with a status code (401/403) instead of redirecting to the login page.
    const fetchHeaders = { "X-Cstic-Fetch": "1" };
    let busy = false;

    function statusMessage(status) {
        if (status === 401) return "Sua sessão expirou. Entre novamente em outra aba (endereço /entrar) e tente de novo; seus campos continuam preenchidos.";
        if (status === 403) return "Sem permissão para esta ação. Se o administrador redefiniu sua senha, entre novamente em outra aba e troque a senha.";
        if (status === 413) return "O conteúdo ficou muito extenso. Reduza o texto e tente novamente.";
        if (status === 429) return "Muitas solicitações. Aguarde um minuto e tente novamente.";
        return null;
    }

    async function readJson(response) {
        return response.headers.get("content-type")?.includes("application/json") ? await response.json() : null;
    }

    const importForm = document.getElementById("import-form");
    if (importForm) {
        const importButton = document.getElementById("import-document");
        const fileInput = document.getElementById("existing-file");
        const importMessage = document.getElementById("import-message");
        const importNotice = (text, error = false) => {
            importMessage.textContent = text;
            importMessage.className = error ? "notice error" : "notice";
            importMessage.setAttribute("role", error ? "alert" : "status");
            importMessage.hidden = false;
            importMessage.focus();
        };
        importForm.addEventListener("submit", async event => {
            event.preventDefault();
            if (busy) return;
            const file = fileInput.files[0];
            if (!file) { importNotice("Selecione o arquivo Word que deseja validar.", true); return; }
            if (!file.name.toLowerCase().endsWith(".docx")) {
                importNotice("Envie um arquivo .docx no modelo CSTIC. PDF e .doc não são aceitos.", true); return;
            }
            if (file.size > 2 * 1024 * 1024) { importNotice("O arquivo deve ter no máximo 2 MB.", true); return; }
            busy = true;
            importButton.disabled = true;
            importButton.textContent = "Lendo documento…";
            try {
                const response = await fetch(importForm.action, { method: "POST", body: new FormData(importForm), credentials: "same-origin", headers: fetchHeaders });
                const info = await readJson(response);
                if (!response.ok) {
                    importNotice(info?.message || statusMessage(response.status) || "Não foi possível importar. Recarregue a página e tente novamente.", true);
                    return;
                }
                window.location.assign(info.redirect);
            } catch {
                importNotice("Não foi possível enviar o arquivo. Confira a conexão e tente novamente.", true);
            } finally {
                busy = false;
                importButton.disabled = false;
                importButton.textContent = "Carregar para revisão";
            }
        });
    }

    const form = document.getElementById("article-form");
    if (!form) return;
    const actionsList = document.getElementById("actions-list");
    const message = document.getElementById("form-message");
    const submitButton = document.getElementById("generate-document");
    const saveButton = document.getElementById("save-draft");
    const downloadButton = document.getElementById("download-word");
    const maxPayload = Number(form.dataset.maxPayload);
    let popId = form.dataset.popId || null;
    let sequence = 0;
    let edited = false;
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
        const executor = document.getElementById("executor").value;
        document.getElementById("summary-title").textContent = title || "Novo procedimento";
        document.getElementById("summary-executor").textContent = executor;
        document.getElementById("executor-summary").textContent = `${executor}, conforme selecionado na identificação.`;
        const required = [...form.querySelectorAll("[required]")];
        const complete = required.filter(input => input.value.trim() && input.validity.valid).length;
        const percentage = Math.round(complete / required.length * 100);
        document.getElementById("form-progress").value = percentage;
        document.getElementById("progress-percentage").textContent = `${percentage}%`;
        document.getElementById("progress-description").textContent = `${complete} de ${required.length} campos obrigatórios preenchidos.`;
    }

    function fill(article) {
        const executor = document.getElementById("executor");
        executor.querySelectorAll("[data-imported]").forEach(option => option.remove());
        if (article.executor && ![...executor.options].some(option => option.value === article.executor)) {
            const option = new Option(`${article.executor} — selecione N1 ou N2`, article.executor);
            option.dataset.imported = "true";
            executor.add(option);
        }
        form.querySelectorAll("[name][data-path]").forEach(input => input.value = article[input.name] ?? "");
        actionsList.replaceChildren();
        (article.actions || []).forEach(action => addAction(action.title, action.steps?.length ? action.steps : undefined));
        if (!actions().length) addAction();
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

    function serialized() {
        const json = JSON.stringify(payload());
        if (json.length > maxPayload) {
            notify(`O POP excede o limite total de ${maxPayload.toLocaleString("pt-BR")} caracteres (atual: ${json.length.toLocaleString("pt-BR")}). Reduza os textos ou divida o conteúdo em mais de um POP.`, true);
            return null;
        }
        return json;
    }

    function post(handler, json) {
        const data = new FormData();
        data.set("Payload", json);
        data.set("__RequestVerificationToken", form.querySelector('[name="__RequestVerificationToken"]').value);
        return fetch(`${window.location.pathname}?handler=${handler}`, { method: "POST", body: data, credentials: "same-origin", headers: fetchHeaders });
    }

    function remember(id) {
        if (!id || String(id) === String(popId)) return;
        popId = String(id);
        form.dataset.popId = popId;
        window.history.replaceState(null, "", `/pops/${popId}/editar`);
    }

    // Runs one request at a time and keeps the fields when it fails.
    async function run(button, label, task) {
        if (busy) return;
        busy = true;
        [submitButton, saveButton, downloadButton].forEach(item => item.disabled = true);
        const text = button.querySelector("span") || button;
        const original = text.textContent;
        text.textContent = label;
        try {
            await task();
        } catch {
            notify("Não foi possível concluir. Confira sua conexão e tente novamente; seus campos continuam preenchidos.", true);
        } finally {
            busy = false;
            [submitButton, saveButton, downloadButton].forEach(item => item.disabled = false);
            text.textContent = original;
        }
    }

    async function failure(response) {
        const info = await readJson(response);
        if (info?.id) remember(info.id);
        if (info?.errors) showValidation(info);
        else notify(info?.message || statusMessage(response.status) || "Não foi possível concluir. Tente novamente em instantes.", true);
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
        document.querySelectorAll("#executor [data-imported]").forEach(option => option.remove());
        form.reset();
        actionsList.replaceChildren();
        addAction();
        clearErrors();
        edited = true;
        update();
        document.getElementById("title").focus();
    });

    document.getElementById("fill-example").addEventListener("click", () => {
        if (edited && !window.confirm("Substituir os dados preenchidos pelo exemplo?")) return;
        const example = {
            title: "Manutenção preventiva de computador administrativo",
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

    saveButton.addEventListener("click", () => {
        clearErrors();
        const json = serialized();
        if (!json) return;
        run(saveButton, "Salvando…", async () => {
            const response = await post("Save", json);
            if (!response.ok) { await failure(response); return; }
            const info = await response.json();
            remember(info.id);
            if (JSON.stringify(payload()) === json) edited = false;
            notify(info.message);
        });
    });

    form.addEventListener("submit", event => {
        event.preventDefault();
        clearErrors();
        if (!form.reportValidity()) return;
        const json = serialized();
        if (!json) return;
        run(submitButton, "Enviando…", async () => {
            const response = await post("Submit", json);
            if (!response.ok) { await failure(response); return; }
            const info = await response.json();
            edited = false;
            window.location.assign(info.redirect);
        });
    });

    downloadButton.addEventListener("click", () => {
        clearErrors();
        if (!form.reportValidity()) return;
        const json = serialized();
        if (!json) return;
        run(downloadButton, "Gerando Word…", async () => {
            const response = await post("Download", json);
            if (!response.ok) { await failure(response); return; }
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
            notify("Word gerado com Nº e aprovador pendentes. O documento oficial é o baixado após a validação.");
        });
    });

    window.addEventListener("beforeunload", event => {
        if (edited) event.preventDefault();
    });

    const initial = JSON.parse(document.getElementById("initial-data").textContent);
    if (initial) fill(initial);
    else addAction();
    update();
})();
