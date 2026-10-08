# Validação da entrega

## Ambiente usado

Linux Debian 13 x64, SDK .NET **10.0.401**, runtime ASP.NET Core **10.0.12**,
Chromium e LibreOffice disponíveis na máquina de desenvolvimento.
O SDK foi baixado da Microsoft e verificado pelo SHA-512 dos metadados oficiais.

## Verificações concluídas

| Verificação | Resultado |
|---|---|
| Build Release | Passou; zero erros e zero avisos de compilação. |
| Executável de testes C# | 30 de 30 verificações passaram. |
| Teste real de navegador/HTTP | 70 de 70 verificações passaram. |
| Download pelo navegador | `.docx` válido, com conteúdo preenchido e rodapé dinâmico. |
| Importação e revisão | Campos, ações/subetapas/detalhes preservados; edição e novo download conferidos. |
| Rejeição de uploads | Arquivo inválido, formato/modelo incorreto, texto/ZIP excessivo, DTD, duplicatas, imagens e alterações controladas rejeitados. |
| Numeração | Ações 1º–4º e reinício em 1.1 dentro de cada ação confirmados no arquivo. |
| Validação de campos no servidor | Obrigatórios, executor, listas vazias/nulas, tamanho e JSON inválido rejeitados. |
| Antiforgery | POST sem token recebeu HTTP 400. |
| Uso simultâneo | 12 gerações independentes verificadas no teste C#. |
| Celular | Largura de 390 px, sem rolagem horizontal e sem erro JavaScript. |
| Base revisada | Word gerado após importação/edição aberto no LibreOffice; PDF A4 de uma página com nova subetapa 1.5. |
| Documento de exemplo | Aberto no LibreOffice e renderizado em PDF A4 de uma página. |
| Documento longo | 80 subetapas, PDF de sete páginas; último item e cabeçalho em todas as páginas conferidos. |
| `dotnet publish` | Publicação Release concluída. |
| Execução da publicação | Health, página, JavaScript e download real do Word passaram. |
| Script shell | Sintaxe de `iniciar.sh` verificada. |
| Docker Compose | Configuração validada com domínio de exemplo informado. |

## Verificações que não foram concluídas

- **Build/execução Docker:** o download das imagens oficiais Microsoft foi
  bloqueado pelo proxy de rede deste ambiente, com HTTP 403 ao domínio
  `westcentralus.data.mcr.microsoft.com`. Foi salva a adição desse domínio no
  rascunho de configuração, preservando as regras existentes; a liberação ainda
  não foi observada na máquina. A falha ocorreu antes de compilar o código
  dentro do Docker. Dockerfile/Compose foram entregues, mas a imagem/container
  não foram validados como funcionais nesta máquina. A execução direta .NET foi.
- **IIS e PowerShell:** arquivos/instruções fornecidos para Windows; não houve
  execução em Windows/IIS nesta máquina Linux.
- **Publicação online, DNS e HTTPS público:** instruções fornecidas, sem
  implantação em um domínio/servidor real do hospital.
- **Microsoft Word:** o DOCX foi validado estruturalmente e aberto no LibreOffice;
  não houve execução do Microsoft Word nesta máquina. Pequenas diferenças de
  paginação entre editores/fontes são possíveis.

Esses itens não são apresentados como testes aprovados. O fluxo local validado
é criar ou importar uma base existente, revisar no navegador e baixar Word a partir do template enviado.

## Reproduzir

```bash
dotnet run --project tests/Cstic.KnowledgeBase.Tests -c Release
dotnet run --project src/Cstic.KnowledgeBase -c Release --urls http://localhost:5080
```

Em outro terminal, com Python/Playwright instalados:

```bash
python tests/browser_smoke.py --url http://localhost:5080
```

Para usar um Chromium já instalado no sistema, acrescente
`--browser /caminho/do/chromium`.

Para gerar um Word de exemplo pelo executável de testes:

```bash
dotnet run --project tests/Cstic.KnowledgeBase.Tests -c Release -- --example artifacts/Exemplo_POP_CSTIC.docx
```

Para conferir a renderização, com LibreOffice instalado:

```bash
soffice --headless --convert-to pdf --outdir artifacts artifacts/Exemplo_POP_CSTIC.docx
```

As pastas `bin`, `obj` e `artifacts` do projeto são ignoradas pelo Git.
O ZIP de entrega contém o código, guia e exemplos, sem essas pastas geradas.
