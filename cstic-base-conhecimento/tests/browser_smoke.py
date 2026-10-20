"""Functional browser/HTTP checks. Optional developer tool, not an app dependency.

Run against a site started with an empty data folder, for example:
    Dados__Pasta=/tmp/cstic-teste dotnet run --project src/Cstic.KnowledgeBase -c Release --urls http://127.0.0.1:5080
"""
import argparse
import copy
import io
import json
import re
from pathlib import Path
from zipfile import ZipFile
import xml.etree.ElementTree as ET

from playwright.sync_api import sync_playwright

parser = argparse.ArgumentParser()
parser.add_argument("--url", default="http://127.0.0.1:5080")
parser.add_argument("--browser", help="Optional path to a system Chromium executable")
parser.add_argument("--output", default="artifacts/browser")
args = parser.parse_args()
base = args.url.rstrip("/")
output = Path(args.output)
output.mkdir(parents=True, exist_ok=True)
ns = {"w": "http://schemas.openxmlformats.org/wordprocessingml/2006/main"}
checks = 0
ADMIN_PASSWORD = "Painel#Cstic2026"
TECH_PASSWORD = "Manutencao2026"


def check(condition, message):
    global checks
    assert condition, message
    checks += 1
    print("PASS", message)


def doc_text(path, name):
    with ZipFile(path) as archive:
        tree = ET.fromstring(archive.read(name))
    return " ".join(node.text or "" for node in tree.findall(".//w:t", ns))


def new_page(browser, errors, width=1440):
    context = browser.new_context(viewport={"width": width, "height": 1000}, accept_downloads=True)
    page = context.new_page()
    page.on("pageerror", lambda error: errors.append(str(error)))
    page.on("console", lambda message: errors.append(message.text) if message.type == "error" and not re.search(r"status of 40[01]", message.text) else None)
    page.on("dialog", lambda dialog: dialog.accept())
    return context, page


def login(page, user, password):
    page.goto(base + "/entrar")
    page.locator("#login").fill(user)
    page.locator("#password").fill(password)
    page.get_by_role("button", name="Entrar").click()
    page.wait_for_load_state()


def change_password(page, current, new):
    page.locator("#current").fill(current)
    page.locator("#password").fill(new)
    page.locator("#confirmation").fill(new)
    page.get_by_role("button", name="Salvar nova senha").click()
    page.wait_for_load_state()


def notice(page):
    return page.locator(".notice").first.inner_text()


def save_download(page, trigger, name):
    with page.expect_download() as event:
        trigger()
    path = output / name
    event.value.save_as(path)
    return path


with sync_playwright() as playwright:
    options = {"headless": True}
    if args.browser:
        options["executable_path"] = args.browser
    browser = playwright.chromium.launch(**options)
    errors = []

    # --- Administrator first access -------------------------------------------------------------
    admin_context, admin = new_page(browser, errors)
    admin.goto(base + "/")
    check(admin.url.startswith(base + "/entrar"), "Visitante é levado à tela de login")
    check(admin.title() == "Entrar · CSTIC", "Tela de login disponível")
    admin.screenshot(path=str(output / "tela-login.png"))
    login(admin, "adm.audi", "senha-errada1")
    check("incorretos" in notice(admin), "Senha incorreta recusada")
    login(admin, "adm.audi", "Senha@123456")
    check(admin.url == base + "/conta/senha", "Primeiro acesso exige troca de senha")
    admin.goto(base + "/admin/pops")
    check(admin.url == base + "/conta/senha", "Painel bloqueado até trocar a senha")
    change_password(admin, "Senha@123456", "Senha@123456")
    check("diferente" in notice(admin), "Nova senha precisa ser diferente")
    change_password(admin, "Senha@123456", ADMIN_PASSWORD)
    check(admin.url == base + "/admin/pops", "Administrador entra no painel após trocar a senha")
    check(admin.locator(".app-nav").get_by_text("Usuários").count() == 1, "Menu de administração visível")

    # --- Technician registration and approval ----------------------------------------------------
    tech_context, tech = new_page(browser, errors)
    tech.goto(base + "/entrar")
    tech.get_by_role("link", name="Solicitar cadastro").click()
    check(tech.url == base + "/cadastro", "Login oferece cadastro")
    check(tech.locator("#function option").all_inner_texts()[1:] == ["Técnico N1", "Técnico N2", "Supervisor", "Qualidade", "Outro"], "Função em lista fixa")

    def register(name, function, user, password, confirmation):
        tech.goto(base + "/cadastro")
        tech.locator("#fullName").fill(name)
        tech.locator("#function").select_option(function)
        tech.locator("#login").fill(user)
        tech.locator("#password").fill(password)
        tech.locator("#confirmation").fill(confirmation)
        tech.get_by_role("button", name="Enviar cadastro").click()
        tech.wait_for_load_state()

    register("Maria José da Silva", "Técnico N1", "Maria.Silva", TECH_PASSWORD, TECH_PASSWORD + "x")
    check("confirmação" in notice(tech), "Confirmação de senha diferente recusada")
    register("Maria", "Técnico N1", "maria.silva", TECH_PASSWORD, TECH_PASSWORD)
    check("nome completo" in notice(tech), "Nome sem sobrenome recusado")
    register("Maria José da Silva", "Técnico N1", "Maria.Silva", TECH_PASSWORD, TECH_PASSWORD)
    check(tech.url == base + "/entrar" and "maria.silva" in notice(tech), "Cadastro enviado com login normalizado")
    register("Maria Silva Souza", "Técnico N2", "maria.silva", TECH_PASSWORD, TECH_PASSWORD)
    check("já está em uso" in notice(tech), "Login repetido recusado")
    login(tech, "maria.silva", TECH_PASSWORD)
    check("aguarda" in notice(tech), "Cadastro pendente não entra")

    admin.reload()
    check(admin.locator(".app-nav .nav-badge").inner_text() == "1", "Painel indica cadastro pendente")
    admin.goto(base + "/admin/usuarios")
    pending_row = admin.locator("tr", has_text="maria.silva")
    check("Técnico N1" in pending_row.inner_text(), "Cadastro pendente listado com função")
    pending_row.get_by_role("button", name="Aprovar").click()
    check("aprovado" in notice(admin), "Administrador aprova cadastro")

    login(tech, "maria.silva", TECH_PASSWORD)
    check(tech.url == base + "/pops", "Técnico aprovado entra em Meus POPs")
    check(tech.locator(".app-nav").get_by_text("Usuários").count() == 0, "Técnico não vê menu de administração")
    tech.goto(base + "/admin/usuarios")
    check(tech.url == base + "/pops", "Técnico não acessa o painel de administração")

    # --- Editor ----------------------------------------------------------------------------------
    tech.goto(base + "/pops/novo")
    check(tech.locator("#author").input_value() == "Maria José da Silva", "Autor preenchido com o técnico")
    check(tech.locator("#code").input_value() == "Automático na validação" and tech.locator("#version").input_value() == "1.0", "Número e versão automáticos")
    check(tech.locator(".procedure-action").count() == 1 and tech.locator(".procedure-step").count() == 1, "Uma ação e uma subetapa iniciais")
    check(tech.locator('[data-action="remove-action"]').is_disabled() and tech.locator('[data-action="remove-step"]').is_disabled(), "Última ação e subetapa protegidas")
    tech.locator("#generate-document").click()
    check(not tech.locator("#title").evaluate("element => element.validity.valid"), "Obrigatório vazio bloqueado no navegador")
    action = tech.locator(".procedure-action").first
    action.locator(".action-title").fill("Primeira ação")
    action.locator(".step-instruction").fill("Primeira subetapa")
    action.locator('[data-action="add-substep"]').click()
    action.locator(".step-instruction").nth(1).fill("Segunda subetapa")
    check(action.locator(".step-number").all_text_contents() == ["1.1", "1.2"], "Subetapas numeradas")
    action.locator('[data-action="up"]').nth(1).click()
    check(action.locator(".step-instruction").first.input_value() == "Segunda subetapa", "Reordenar preserva a descrição")
    action.locator('[data-action="remove-step"]').nth(1).click()
    check(action.locator(".step-number").all_text_contents() == ["1.1"], "Remover renumera subetapas")
    action.locator('[data-action="add-detail"]').click()
    action.locator(".detail-text").fill("Detalhe opcional")
    action.locator('[data-action="remove-detail"]').click()
    check(action.locator(".detail-row").count() == 0, "Adicionar e remover detalhe")
    tech.locator("#add-action").click()
    second = tech.locator(".procedure-action").nth(1)
    second.locator(".action-title").fill("Segunda ação")
    second.locator(".step-instruction").fill("Subetapa da segunda ação")
    check(tech.locator(".step-number").all_text_contents() == ["1.1", "1.1"], "Cada ação recomeça em 1.1")
    second.locator('[data-action="action-up"]').click()
    check(tech.locator(".action-title").first.input_value() == "Segunda ação", "Reordenar ações preserva suas subetapas")
    tech.locator(".procedure-action").nth(1).locator('[data-action="remove-action"]').click()
    check(tech.locator(".action-number").all_text_contents() == ["1º"], "Remover renumera ações")

    tech.locator("#fill-example").click()
    check(tech.locator(".procedure-action").count() == 4 and tech.locator(".procedure-step").count() == 11, "Exemplo com quatro ações e onze subetapas")
    check(tech.locator("#progress-percentage").inner_text() == "100%", "Progresso completo")
    draft_word = save_download(tech, lambda: tech.locator("#download-word").click(), "POP_rascunho.docx")
    header = doc_text(draft_word, "word/header3.xml")
    body = doc_text(draft_word, "word/document.xml")
    check("PENDENTE" in header and "Pendente de validação" in header and "Maria José da Silva" in header, "Word do rascunho traz número e aprovador pendentes")
    check("1º  Abrir computador" in body and body.count("1.1  ") == 4 and "1.4  Testar" in body, "Numeração correta no Word")
    check("Manutenção preventiva" in doc_text(draft_word, "word/footer3.xml"), "Rodapé recebe o nome do POP")

    tech.locator("#save-draft").click()
    tech.wait_for_url(re.compile(r"/pops/\d+/editar$"))
    pop_url = tech.url.removesuffix("/editar")
    check("Rascunho salvo" in tech.locator("#form-message").inner_text(), "Rascunho salvo")
    tech.reload()
    check(tech.locator(".procedure-step").count() == 11 and tech.locator("#title").input_value() == "Manutenção preventiva de computador administrativo", "Rascunho reaberto com o conteúdo salvo")

    payload = tech.evaluate("""() => {
        const article = {};
        document.querySelectorAll('[name][data-path]').forEach(input => article[input.name] = input.value);
        article.actions = [...document.querySelectorAll('.procedure-action')].map(action => ({
            title: action.querySelector('.action-title').value,
            steps: [...action.querySelectorAll('.procedure-step')].map(step => ({
                instruction: step.querySelector('.step-instruction').value,
                details: [...step.querySelectorAll('.detail-text')].map(input => input.value)
            }))
        }));
        return article;
    }""")
    token = tech.locator('#article-form [name="__RequestVerificationToken"]').input_value()
    endpoint = base + "/pops/novo?handler=Download"
    response = tech_context.request.post(endpoint, form={"Payload": json.dumps(payload)})
    check(response.status == 400, "POST sem antiforgery é rejeitado")
    for label, modify, field in [
        ("Título vazio", lambda article: article.update(title=""), "title"),
        ("Executor inválido", lambda article: article.update(executor="N3"), "executor"),
        ("Ações vazias", lambda article: article.update(actions=[]), "actions"),
        ("Ação sem subetapa", lambda article: article["actions"][0].update(steps=[]), "actions[0].steps"),
        ("Subetapa vazia", lambda article: article["actions"][0]["steps"][0].update(instruction=" "), "actions[0].steps[0].instruction"),
    ]:
        invalid = copy.deepcopy(payload)
        modify(invalid)
        response = tech_context.request.post(endpoint, form={"Payload": json.dumps(invalid), "__RequestVerificationToken": token})
        check(response.status == 400 and field in response.json()["errors"], label + " rejeitado pelo servidor")
    response = tech_context.request.post(endpoint, form={"Payload": "{broken", "__RequestVerificationToken": token})
    check(response.status == 400, "JSON malformado rejeitado")
    long = copy.deepcopy(payload)
    for action_index, item in enumerate(long["actions"]):
        item["steps"] = [{"instruction": f"Ação {action_index + 1}, subetapa {index + 1}: " + "Texto de validação de paginação. " * 8, "details": []} for index in range(20)]
    response = tech_context.request.post(endpoint, form={"Payload": json.dumps(long), "__RequestVerificationToken": token})
    check(response.status == 200, "Documento longo de 80 subetapas gerado")
    oversized = copy.deepcopy(payload)
    oversized["objective"] = "a" * 180_001
    response = tech_context.request.post(endpoint, form={"Payload": json.dumps(oversized), "__RequestVerificationToken": token})
    check(response.status == 400 and "180.000" in response.json()["message"], "Limite total do POP informado pelo servidor")
    anonymous = browser.new_context()
    response = anonymous.request.post(base + "/pops/novo?handler=Save", form={"Payload": json.dumps(payload)}, max_redirects=0)
    check(response.status in (302, 400), "Visitante sem login não salva POP")
    anonymous.close()

    requests = []
    tech.on("request", lambda request: requests.append(request.url) if "handler=" in request.url else None)
    tech.evaluate("""() => {
        for (const step of document.querySelectorAll('.procedure-step')) {
            const button = step.querySelector('[data-action="add-detail"]');
            while (!button.disabled) button.click();
            step.querySelectorAll('.detail-text').forEach(input => input.value = 'x'.repeat(900));
        }
        document.getElementById('article-form').dispatchEvent(new Event('input'));
    }""")
    tech.locator("#save-draft").click()
    check("excede o limite total" in tech.locator("#form-message").inner_text() and not requests, "POP acima do limite total bloqueado no navegador")
    tech.reload()
    tech.evaluate("window.scrollTo(0, 0)")
    tech.screenshot(path=str(output / "interface-desktop.png"))
    tech.set_viewport_size({"width": 390, "height": 844})
    check(tech.evaluate("document.documentElement.scrollWidth <= window.innerWidth"), "Editor no celular sem rolagem horizontal")
    tech.screenshot(path=str(output / "interface-mobile.png"))
    tech.set_viewport_size({"width": 1440, "height": 1000})

    tech.locator("#generate-document").click()
    tech.wait_for_url(pop_url)
    check("enviado para validação" in notice(tech) and "Aguardando validação" in tech.locator(".status-badge").first.inner_text(), "POP enviado para validação")
    check(tech.get_by_role("link", name="Editar").count() == 0, "POP enviado não pode ser editado")
    tech.goto(pop_url + "/editar")
    check(tech.url == pop_url, "Edição de POP enviado redireciona para a visualização")

    # --- Administrator returns, technician fixes, administrator validates ------------------------
    admin.goto(base + "/admin/pops")
    row = admin.locator("tr", has_text="Manutenção preventiva")
    check("Maria José da Silva" in row.inner_text() and "Técnico N1" in row.inner_text(), "POP recebido com o nome do técnico")
    check(admin.locator(".app-nav .nav-badge").inner_text() == "1", "Painel indica POP aguardando")
    row.get_by_role("link", name="Analisar").click()
    check(admin.locator(".pop-preview").count() == 1 and "Abrir computador" in admin.locator(".pop-preview").inner_text(), "Administrador visualiza o conteúdo")
    admin.screenshot(path=str(output / "analise-administrador.png"), full_page=True)
    admin.locator("#comment").fill("Detalhar a limpeza com pincel antiestático.")
    admin.get_by_role("button", name="Devolver ao técnico").click()
    check(admin.url == base + "/admin/pops" and "devolvido" in notice(admin), "POP devolvido com comentário")

    tech.goto(base + "/pops")
    check("Devolvido para ajuste" in tech.locator("tbody tr").first.inner_text() and "pincel antiestático" in tech.locator(".return-hint").inner_text(), "Técnico vê a devolução e o motivo")
    tech.get_by_role("link", name="Editar").click()
    check("pincel antiestático" in tech.locator(".notice.warning").inner_text(), "Editor mostra o ajuste solicitado")
    last_action = tech.locator(".procedure-action").last
    last_action.locator('[data-action="add-substep"]').click()
    last_action.locator(".step-instruction").last.fill("Registrar a conclusão da revisão.")
    tech.locator("#generate-document").click()
    tech.wait_for_url(pop_url)
    check("Aguardando validação" in tech.locator(".status-badge").first.inner_text(), "POP reenviado")

    admin.goto(pop_url)
    check("versão 1.0" in admin.locator(".review-panel").inner_text(), "Validação informa a versão")
    admin.get_by_role("button", name="Validar POP").click()
    check("Nº 0001, versão 1.0" in notice(admin), "Validação atribui número e versão automáticos")
    validated = save_download(admin, lambda: admin.get_by_role("link", name="Baixar Word").click(), "POP_validado.docx")
    header = doc_text(validated, "word/header3.xml")
    check("0001" in header and "1.0" in header and "Administrador CSTIC" in header and "Maria José da Silva" in header, "Word validado com número, versão, autor e aprovador")
    check("1.5  Registrar a conclusão" in doc_text(validated, "word/document.xml"), "Ajuste do técnico no Word validado")

    # --- Revision of a validated POP --------------------------------------------------------------
    tech.goto(base + "/pops")
    check(re.search(r"0001.*1\.0.*Validado", tech.locator("tbody tr").first.inner_text(), re.S) is not None, "Técnico vê POP validado")
    tech.get_by_role("link", name="Revisar").click()
    check("versão 1.1" in tech.locator(".notice").first.inner_text() and tech.locator("#version").input_value() == "1.1", "Revisão prevê a próxima versão")
    tech.locator("#title").fill("Manutenção preventiva — revisão")
    tech.locator("#save-draft").click()
    tech.locator("#form-message").wait_for()
    tech.goto(pop_url)
    check("Revisão em rascunho" in tech.locator(".status-badge").first.inner_text(), "Alteração inicia uma revisão")
    old = save_download(tech, lambda: tech.get_by_role("link", name="Versão 1.0").click(), "POP_versao_1_0.docx")
    check("Manutenção preventiva de computador administrativo" in doc_text(old, "word/header3.xml"), "Versão validada anterior continua disponível")
    tech.get_by_role("link", name="Editar").click()
    tech.locator("#generate-document").click()
    tech.wait_for_url(pop_url)
    admin.goto(pop_url)
    admin.get_by_role("button", name="Validar POP").click()
    check("Nº 0001, versão 1.1" in notice(admin), "Revisão validada como versão 1.1")

    # --- Import ----------------------------------------------------------------------------------
    tech.goto(base + "/pops/importar")
    check(".docx" in tech.locator("#upload-help").inner_text() and "2 MB" in tech.locator("#upload-help").inner_text(), "Formato e tamanho informados")
    tech.locator("#import-document").click()
    check("Selecione" in tech.locator("#import-message").inner_text(), "Upload sem arquivo orienta técnico")
    tech.locator("#existing-file").set_input_files({"name": "base.pdf", "mimeType": "application/pdf", "buffer": b"%PDF-1.7"})
    tech.locator("#import-document").click()
    check("PDF e .doc" in tech.locator("#import-message").inner_text(), "PDF rejeitado com aviso claro")
    tech.locator("#existing-file").set_input_files({"name": "grande.docx", "mimeType": "application/octet-stream", "buffer": bytes(2 * 1024 * 1024 + 1)})
    tech.locator("#import-document").click()
    check("2 MB" in tech.locator("#import-message").inner_text(), "Upload acima de 2 MB bloqueado no navegador")
    tech.locator("#existing-file").set_input_files({"name": "invalido.docx", "mimeType": "application/octet-stream", "buffer": b"invalid zip"})
    with tech.expect_response(lambda response: "handler=Import" in response.url) as invalid_response:
        tech.locator("#import-document").click()
    check(invalid_response.value.status == 400, "Word corrompido rejeitado pelo servidor")
    import_endpoint = base + "/pops/importar?handler=Import"
    import_token = tech.locator('#import-form [name="__RequestVerificationToken"]').input_value()
    response = tech_context.request.post(import_endpoint, multipart={"UploadedFile": {"name": "base.docx", "mimeType": "application/octet-stream", "buffer": validated.read_bytes()}})
    check(response.status == 400, "Upload sem antiforgery rejeitado")
    response = tech_context.request.post(import_endpoint, multipart={"__RequestVerificationToken": import_token, "UploadedFile": {"name": "base.pdf", "mimeType": "application/pdf", "buffer": b"%PDF"}})
    check(response.status == 400 and ".docx" in response.json()["message"], "Extensão inválida rejeitada pelo servidor")
    with ZipFile(validated) as source:
        unusual = io.BytesIO()
        with ZipFile(unusual, "w") as target:
            for item in source.infolist():
                data = source.read(item.filename)
                if item.filename == "word/document.xml":
                    data = data.replace(b">N2 / N1<", b">N3<", 1)
                target.writestr(item, data)
    tech.locator("#existing-file").set_input_files({"name": "executor.docx", "mimeType": "application/octet-stream", "buffer": unusual.getvalue()})
    tech.locator("#import-document").click()
    tech.wait_for_url(re.compile(r"/pops/\d+/editar$"))
    check("Arquivo carregado: executor.docx" in notice(tech), "Word importado vira rascunho do técnico")
    check(tech.locator("#title").input_value() == "Manutenção preventiva de computador administrativo", "Título importado")
    check(tech.locator(".procedure-step").count() == 12, "Importação conserva ações e subetapas")
    check(tech.locator("#code").input_value() == "Automático na validação", "Número já usado não é reaproveitado na importação")
    check(tech.locator("#version").input_value() == "1.1", "Versão do arquivo é a base da próxima")
    check(tech.locator("#executor [data-imported]").count() == 1, "Executor fora do padrão aparece para correção")
    tech.locator("#clear-form").click()
    check(tech.locator("#executor [data-imported]").count() == 0 and tech.locator(".procedure-step").count() == 1, "Limpar formulário remove conteúdo e executor importado")

    # --- User administration ---------------------------------------------------------------------
    admin.goto(base + "/admin/usuarios")
    admin.locator("#new-name").fill("João Pedro Lima")
    admin.locator("#new-function").select_option("Técnico N2")
    admin.locator("#new-login").fill("joao.lima")
    admin.locator("#new-password").fill("Temporaria2026")
    admin.locator("#new-confirmation").fill("Temporaria2026")
    admin.get_by_role("button", name="Criar cadastro").click()
    check("joao.lima" in notice(admin), "Administrador cria cadastro ativo")
    other_context, other = new_page(browser, errors)
    login(other, "joao.lima", "Temporaria2026")
    check(other.url == base + "/conta/senha", "Cadastro criado pelo administrador troca a senha no primeiro acesso")

    maria = admin.locator(".user-row", has_text="maria.silva")
    maria.locator("summary", has_text="Redefinir senha").click()
    maria.locator('input[name="password"]').fill("Provisoria2026")
    maria.locator('input[name="confirmation"]').fill("Provisoria2026")
    maria.get_by_role("button", name="Redefinir senha").click()
    check("redefinida" in notice(admin), "Administrador redefine senha sem ver a atual")
    tech.goto(base + "/pops")
    check(tech.url.startswith(base + "/entrar"), "Redefinição encerra a sessão do técnico")
    login(tech, "maria.silva", TECH_PASSWORD)
    check("incorretos" in notice(tech), "Senha antiga deixa de valer")
    login(tech, "maria.silva", "Provisoria2026")
    check(tech.url == base + "/conta/senha", "Senha redefinida exige troca")
    change_password(tech, "Provisoria2026", TECH_PASSWORD)
    check(tech.url == base + "/pops", "Técnico volta após definir nova senha")

    joao = admin.locator(".user-row", has_text="joao.lima")
    joao.get_by_role("button", name="Desativar").click()
    check("desativado" in notice(admin), "Administrador desativa acesso")
    other.goto(base + "/conta/senha")
    check(other.url.startswith(base + "/entrar"), "Desativação encerra a sessão")
    login(other, "joao.lima", "Temporaria2026")
    check("desativado" in notice(other), "Usuário desativado não entra")
    check(admin.locator(".user-row", has_text="adm.audi").get_by_role("button", name="Desativar").count() == 0, "Administrador não pode desativar a si mesmo")
    for attempt in range(5):
        login(other, "joao.lima", f"errada{attempt}abc")
    login(other, "joao.lima", "Temporaria2026")
    check("Muitas tentativas" in notice(other), "Bloqueio após cinco senhas incorretas")

    admin.set_viewport_size({"width": 390, "height": 844})
    check(admin.evaluate("document.documentElement.scrollWidth <= window.innerWidth"), "Painel de usuários no celular sem rolagem horizontal")
    admin.screenshot(path=str(output / "usuarios-mobile.png"), full_page=True)
    admin.goto(base + "/admin/pops?situacao=validados")
    check(admin.evaluate("document.documentElement.scrollWidth <= window.innerWidth"), "POPs recebidos no celular sem rolagem horizontal")
    admin.locator("form[action='/sair'] button").first.click()
    check(admin.url == base + "/entrar" and "saiu" in notice(admin), "Sair encerra a sessão")

    check(not errors, "Nenhum erro JavaScript: " + str(errors))
    browser.close()

print(f"{checks} verificações funcionais passaram.")
