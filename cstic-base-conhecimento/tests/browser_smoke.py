"""Functional browser/HTTP checks. Optional developer tool, not an app dependency."""
import argparse
import copy
import io
import json
from pathlib import Path
from zipfile import ZipFile
import xml.etree.ElementTree as ET

from playwright.sync_api import sync_playwright

parser = argparse.ArgumentParser()
parser.add_argument("--url", default="http://127.0.0.1:5080")
parser.add_argument("--browser", help="Optional path to a system Chromium executable")
parser.add_argument("--output", default="artifacts/browser")
args = parser.parse_args()
output = Path(args.output)
output.mkdir(parents=True, exist_ok=True)
ns = {"w": "http://schemas.openxmlformats.org/wordprocessingml/2006/main"}
checks = 0


def check(condition, message):
    global checks
    assert condition, message
    checks += 1
    print("PASS", message)


def doc_text(archive, name):
    tree = ET.fromstring(archive.read(name))
    return " ".join(node.text or "" for node in tree.findall(".//w:t", ns))


with sync_playwright() as playwright:
    options = {"headless": True}
    if args.browser:
        options["executable_path"] = args.browser
    browser = playwright.chromium.launch(**options)
    context = browser.new_context(viewport={"width": 1440, "height": 1000}, accept_downloads=True)
    page = context.new_page()
    errors = []
    page.on("pageerror", lambda error: errors.append(str(error)))
    page.on("console", lambda message: errors.append(message.text) if message.type == "error" and "status of 400" not in message.text else None)
    page.goto(args.url)
    check(page.title() == "Base de conhecimento · CSTIC", "Página disponível")
    check(page.get_by_role("link", name="Criar base de conhecimento").count() == 1, "Entrada oferece criação")
    check(page.get_by_role("link", name="Validar base existente").count() == 1, "Entrada oferece validação")
    page.screenshot(path=str(output / "interface-inicial.png"))
    page.get_by_role("link", name="Criar base de conhecimento").click()
    check(page.locator(".procedure-action").count() == 1, "Uma ação inicial")
    check(page.locator(".procedure-step").count() == 1, "Uma subetapa inicial")
    check(page.locator('[data-action="remove-action"]').is_disabled(), "Última ação protegida")
    check(page.locator('[data-action="remove-step"]').is_disabled(), "Última subetapa protegida")
    page.locator("#generate-document").click()
    check(not page.locator("#code").evaluate("element => element.validity.valid"), "Obrigatório vazio bloqueado no navegador")

    action = page.locator(".procedure-action").first
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

    page.locator("#add-action").click()
    second = page.locator(".procedure-action").nth(1)
    second.locator(".action-title").fill("Segunda ação")
    second.locator(".step-instruction").fill("Subetapa da segunda ação")
    check(page.locator(".step-number").all_text_contents() == ["1.1", "1.1"], "Cada ação recomeça em 1.1")
    second.locator('[data-action="action-up"]').click()
    check(page.locator(".action-title").first.input_value() == "Segunda ação", "Reordenar ações preserva suas subetapas")
    page.locator(".procedure-action").nth(1).locator('[data-action="remove-action"]').click()
    check(page.locator(".action-number").all_text_contents() == ["1º"], "Remover renumera ações")

    page.on("dialog", lambda dialog: dialog.accept())
    page.locator("#fill-example").click()
    check(page.locator(".procedure-action").count() == 4, "Exemplo com quatro ações")
    check(page.locator(".procedure-step").count() == 11, "Exemplo com onze subetapas")
    check(page.locator("#progress-percentage").inner_text() == "100%", "Progresso completo")
    for action in page.locator(".procedure-action").all():
        check(action.locator(".step-number").first.inner_text() == "1.1", "Reinício da numeração na ação do exemplo")

    with page.expect_download() as download_event:
        page.locator("#generate-document").click()
    download = download_event.value
    downloaded = output / "POP_baixado_no_navegador.docx"
    download.save_as(downloaded)
    with ZipFile(downloaded) as archive:
        check(archive.testzip() is None, "Word baixado é um ZIP válido")
        header = doc_text(archive, "word/header3.xml")
        body = doc_text(archive, "word/document.xml")
        footer = doc_text(archive, "word/footer3.xml")
        check("Manutenção preventiva de computador administrativo" in header, "Título preenchido no cabeçalho")
        check("1º  Abrir computador" in body and "4º  Montagem" in body, "Ações exportadas")
        check(body.count("1.1  ") == 4 and "1.4  Testar" in body, "Numeração correta no Word")
        check("Manutenção preventiva" in footer, "Rodapé recebe o nome do POP")
    check(page.locator("#author").input_value() == "Técnico de exemplo", "Download preserva os campos")

    payload = page.evaluate("""() => {
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
    token = page.locator('[name="__RequestVerificationToken"]').input_value()
    endpoint = args.url.rstrip("/") + "/criar?handler=Generate"
    response = context.request.post(endpoint, form={"Payload": json.dumps(payload)})
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
        response = context.request.post(endpoint, form={"Payload": json.dumps(invalid), "__RequestVerificationToken": token})
        check(response.status == 400 and field in response.json()["errors"], label + " rejeitado pelo servidor")
    response = context.request.post(endpoint, form={"Payload": "{broken", "__RequestVerificationToken": token})
    check(response.status == 400, "JSON malformado rejeitado")

    long = copy.deepcopy(payload)
    long["title"] = "Validação de procedimento longo"
    for action_index, action in enumerate(long["actions"]):
        action["steps"] = [{"instruction": f"Ação {action_index + 1}, subetapa {index + 1}: " + "Texto de validação de paginação. " * 8, "details": []} for index in range(20)]
    response = context.request.post(endpoint, form={"Payload": json.dumps(long), "__RequestVerificationToken": token})
    check(response.status == 200, "Documento longo de 80 subetapas gerado")
    (output / "POP_longo.docx").write_bytes(response.body())

    page.evaluate("window.scrollTo(0, 0)")
    page.screenshot(path=str(output / "interface-desktop.png"))
    page.set_viewport_size({"width": 390, "height": 844})
    page.evaluate("window.scrollTo(0, 0)")
    check(page.evaluate("document.documentElement.scrollWidth <= window.innerWidth"), "Tela de celular sem rolagem horizontal")
    page.screenshot(path=str(output / "interface-mobile.png"))

    page.set_viewport_size({"width": 1440, "height": 1000})
    page.goto(args.url.rstrip("/") + "/validar")
    check(page.locator("#editor-layout").is_hidden(), "Revisão aguarda upload")
    check(".docx" in page.locator("#upload-help").inner_text() and "2 MB" in page.locator("#upload-help").inner_text(), "Formato e tamanho informados")
    page.locator("#import-document").click()
    check("Selecione" in page.locator("#import-message").inner_text(), "Upload sem arquivo orienta técnico")
    page.locator("#existing-file").set_input_files({"name": "base.pdf", "mimeType": "application/pdf", "buffer": b"%PDF-1.7"})
    page.locator("#import-document").click()
    check("PDF e .doc" in page.locator("#import-message").inner_text(), "PDF rejeitado com aviso claro")
    page.locator("#existing-file").set_input_files({"name": "grande.docx", "mimeType": "application/octet-stream", "buffer": bytes(2 * 1024 * 1024 + 1)})
    page.locator("#import-document").click()
    check("2 MB" in page.locator("#import-message").inner_text(), "Upload acima de 2 MB bloqueado no navegador")
    page.locator("#existing-file").set_input_files(str(downloaded.resolve()))
    with page.expect_response(lambda response: "handler=Import" in response.url) as imported_response:
        page.locator("#import-document").click()
    check(imported_response.value.status == 200, "Word existente importado")
    page.locator("#editor-layout").wait_for(state="visible")
    check(page.locator("#title").input_value() == payload["title"], "Título preenchido automaticamente")
    for field in ["code", "version", "author", "approver", "executor", "objective", "result", "requirements"]:
        check(page.locator("#" + field).input_value() == payload[field], "Importação do campo " + field)
    check(page.locator(".procedure-action").count() == 4 and page.locator(".procedure-step").count() == 11, "Importação conserva ações e subetapas")
    check(page.locator(".detail-text").first.input_value() == payload["actions"][0]["steps"][0]["details"][0], "Importação conserva detalhes")
    for action in page.locator(".procedure-action").all():
        check(action.locator(".step-number").first.inner_text() == "1.1", "Revisão recomeça em 1.1 por ação")
    page.locator("#title").fill("Manutenção preventiva — base revisada")
    page.locator("#version").fill("1.1")
    last_action = page.locator(".procedure-action").last
    last_action.locator('[data-action="add-substep"]').click()
    last_action.locator(".step-instruction").last.fill("Registrar a conclusão da revisão.")
    with page.expect_download() as reviewed_download:
        page.locator("#generate-document").click()
    reviewed_path = output / "POP_revisado.docx"
    reviewed_download.value.save_as(reviewed_path)
    with ZipFile(reviewed_path) as archive:
        check("base revisada" in doc_text(archive, "word/header3.xml") and "1.1" in doc_text(archive, "word/header3.xml"), "Documento revisado contém título e versão atualizados")
        check("1.5  Registrar a conclusão" in doc_text(archive, "word/document.xml"), "Nova subetapa exportada na revisão")
    check(page.locator("#generate-document span").inner_text() == "Gerar base revisada", "Botão mantém ação de revisão")
    page.evaluate("window.scrollTo(0, 0)")
    page.screenshot(path=str(output / "interface-validacao.png"))
    page.locator("#existing-file").set_input_files({"name": "invalido.docx", "mimeType": "application/octet-stream", "buffer": b"invalid zip"})
    with page.expect_response(lambda response: "handler=Import" in response.url) as invalid_response:
        page.locator("#import-document").click()
    check(invalid_response.value.status == 400, "Word corrompido rejeitado pelo servidor")
    check(page.locator("#title").input_value() == "Manutenção preventiva — base revisada", "Falha de importação preserva a revisão atual")
    review_token = page.locator('[name="__RequestVerificationToken"]').input_value()
    import_endpoint = args.url.rstrip("/") + "/validar?handler=Import"
    response = context.request.post(import_endpoint, multipart={"UploadedFile": {"name": "base.docx", "mimeType": "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "buffer": downloaded.read_bytes()}})
    check(response.status == 400, "Upload sem antiforgery rejeitado")
    response = context.request.post(import_endpoint, form={"__RequestVerificationToken": review_token})
    check(response.status == 400 and "Selecione" in response.json()["message"], "Upload vazio rejeitado pelo servidor")
    response = context.request.post(import_endpoint, multipart={"__RequestVerificationToken": review_token, "UploadedFile": {"name": "base.pdf", "mimeType": "application/pdf", "buffer": b"%PDF"}})
    check(response.status == 400 and ".docx" in response.json()["message"], "Extensão inválida rejeitada pelo servidor")
    page.set_viewport_size({"width": 390, "height": 844})
    page.evaluate("window.scrollTo(0, 0)")
    check(page.evaluate("document.documentElement.scrollWidth <= window.innerWidth"), "Revisão no celular sem rolagem horizontal")
    page.screenshot(path=str(output / "interface-validacao-mobile.png"))
    page.locator("#clear-form").click()
    check(page.locator("#editor-layout").is_hidden() and page.locator("#existing-file").input_value() == "", "Limpar revisão permite carregar outra base")
    page.goto(args.url)
    check(page.evaluate("document.documentElement.scrollWidth <= window.innerWidth"), "Página inicial no celular sem rolagem horizontal")
    page.screenshot(path=str(output / "interface-inicial-mobile.png"))

    check(not errors, "Nenhum erro JavaScript: " + str(errors))
    browser.close()

print(f"{checks} verificações funcionais passaram.")
