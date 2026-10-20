# Validação da entrega

## Ambiente usado

Linux x64, SDK .NET **10.0.112**, EF Core **10.0.13** com SQLite, Chromium
(Playwright 1.56) para o teste de navegador. A verificação de vulnerabilidades do
NuGet (`NuGetAudit`) passou sem avisos; o projeto trata avisos como erros.

## Verificações concluídas

| Verificação | Resultado |
|---|---|
| Build Release | Passou; zero erros e zero avisos de compilação. |
| Executável de testes C# | 46 de 46 verificações passaram. |
| Teste real de navegador/HTTP | 101 de 101 verificações passaram, com banco vazio. |
| Primeiro acesso | `adm.audi` / `Senha@123456` entra e é obrigado a trocar a senha antes do painel. |
| Cadastro e aprovação | Login normalizado, lista fixa de funções, senhas fracas/diferentes recusadas, pendente não entra, aprovação libera. |
| Gestão de usuários | Criação com troca no primeiro acesso, redefinição sem exibir a senha (encerra a sessão), desativação, bloqueio após 5 erros, último administrador protegido. |
| Fluxo do POP | Rascunho, envio, bloqueio de edição, devolução com comentário, correção, reenvio e validação. |
| Número e versão | `0001`/1.0 na primeira validação, `0002` no POP seguinte, 1.1 na revisão, versão anterior ainda disponível. |
| Word | Rascunho com Nº e aprovador pendentes; validado com número, versão, autor, aprovador e data da validação. |
| Importação | Vira rascunho do técnico; número livre mantido, número repetido substituído; versão do arquivo como base. |
| Permissões | Técnico não acessa painel nem POPs de outros; visitante não salva; antiforgery exigido. |
| Comando de recuperação | `redefinir-senha adm.audi` gera senha temporária; login inexistente retorna erro. |
| Celular | Largura de 390 px sem rolagem horizontal no editor, POPs recebidos e usuários; sem erro JavaScript. |

## Verificações que não foram concluídas

- **IIS e PowerShell:** instruções fornecidas (incluindo permissão de gravação em
  `App_Data`); não houve execução em Windows/IIS nesta máquina Linux.
- **Docker:** Dockerfile e Compose atualizados com volume para `App_Data`, mas a
  imagem não foi construída nesta máquina.
- **Microsoft Word:** o DOCX foi validado estruturalmente; não houve execução do
  Microsoft Word nesta máquina.

## Reproduzir

```bash
dotnet run --project tests/Cstic.KnowledgeBase.Tests -c Release
Dados__Pasta=/tmp/cstic-teste dotnet run --project src/Cstic.KnowledgeBase -c Release --urls http://localhost:5080
```

Em outro terminal, com Python/Playwright instalados:

```bash
python tests/browser_smoke.py --url http://localhost:5080
```

O teste de navegador espera um banco vazio (usa a senha inicial do administrador).
Apague a pasta indicada em `Dados__Pasta` antes de repeti-lo. Para usar um Chromium
já instalado, acrescente `--browser /caminho/do/chromium`.

Para gerar um Word de exemplo pelo executável de testes:

```bash
dotnet run --project tests/Cstic.KnowledgeBase.Tests -c Release -- --example artifacts/Exemplo_POP_CSTIC.docx
```

As pastas `bin`, `obj`, `artifacts` e `App_Data` são ignoradas pelo Git.
