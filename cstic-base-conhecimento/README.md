# CSTIC · Base de conhecimento

Aplicação web em **C# e ASP.NET Core 10**, com Razor Pages, para criar ou revisar um
Procedimento Operacional Padrão (POP) e baixar um **Word editável `.docx`**.
O documento é gerado a partir do **arquivo original enviado pelo usuário**,
incorporado na aplicação. Não é um HTML renomeado para `.docx`.

**Sem login, sem banco de dados e sem histórico.** O conteúdo preenchido é
processado em memória e enviado ao navegador. A aplicação não salva POPs no servidor.

## Comece aqui

1. Instale o **SDK .NET 10**: https://dotnet.microsoft.com/pt-br/download/dotnet/10.0
2. Abra um terminal na pasta deste projeto.
3. Execute:

```bash
dotnet run --project src/Cstic.KnowledgeBase -c Release --urls http://localhost:5080
```

4. Abra `http://localhost:5080` no navegador.
5. Escolha **Criar base de conhecimento** ou **Validar base existente**.
6. Para criar, preencha o formulário e clique em **Gerar documento Word**.
7. Para validar, envie o `.docx`, confira/edite os campos e clique em **Gerar base revisada**.

No Windows, também pode executar `powershell -ExecutionPolicy Bypass -File .\iniciar.ps1`
(a liberação vale apenas para esse processo). No Linux/macOS: `bash iniciar.sh`.

**Guia completo para iniciantes, rede local, IIS, Docker e publicação online:**
[docs/GUIA-INSTALACAO.md](docs/GUIA-INSTALACAO.md).

## Validar uma base existente

Na tela inicial, clique em **Validar base existente**. Envie um **Word `.docx`
no modelo CSTIC, com até 2 MB**, e clique em **Carregar para revisão**.
O site identifica Nº, Nome, Versão, Autor, Aprovador, Objetivo, Resultado,
Requisitos, Executor, ações, subetapas e detalhes. A data original aparece
na mensagem de importação; o documento final recebe a data atual de Recife.

O formulário só aparece após uma importação bem-sucedida. Você pode editar os
campos, adicionar/remover/reordenar etapas e baixar o **Word revisado**.
Confira autor, aprovador e versão: o sistema preserva esses dados do arquivo,
sem incrementar versão ou registrar aprovação automaticamente.

São aceitos POPs gerados pelo site e documentos **somente com texto** preenchidos
nas cinco tabelas e no cabeçalho do modelo original. Em documentos antigos sem
ações `1º`, `2º`…, o texto é agrupado em “Procedimento importado”, com aviso para
conferir sua organização. Formatação específica do arquivo recebido não é copiada:
o texto revisado volta ao padrão do template CSTIC.

PDF, `.doc`, documentos de outros modelos, imagens/objetos e alterações
controladas não são importados. Uma extensão `.docx` sozinha não basta: a estrutura
é conferida no servidor. Arquivos inválidos ou conteúdo acima dos limites recebem
uma mensagem, sem cortar texto ou apagar uma revisão já carregada.

## Documento e numeração

Cabeçalho: Nº, Nome, Versão, Data criação/atualização, Autor e Aprovador.
Blocos: **1. Objetivo, 2. Resultado, 3. Requisitos, 4. Executor, 5. Procedimento**.

Dentro de **5. Procedimento**, cada ação recebe uma ordem, e **a numeração das
subetapas recomeça em 1.1 dentro de cada ação**, conforme solicitado:

```text
1º Abrir computador
   1.1 Retirar os parafusos
   1.2 Retirar a tampa

2º Limpeza
   1.1 Retirar componentes
   1.2 Realizar a limpeza

3º Preventivas
   1.1 Verificar a pasta térmica
   1.2 Verificar conectores
   1.3 Identificar itens defeituosos

4º Montagem
   1.1 Recolocar as peças
   1.2 Fechar o gabinete
   1.3 Recolocar os parafusos
   1.4 Testar
```

Você pode adicionar/remover/reordenar ações e subetapas. Os detalhes opcionais
viram marcadores abaixo de uma subetapa. Todas as descrições permanecem associadas
à sua ação ao reordenar. A data é gerada no servidor em UTC−3, horário de Recife.
O número do POP é informado pelo técnico: não existe contador central nesta versão.

## Estrutura

```text
cstic-base-conhecimento/
├── README.md
├── global.json
├── iniciar.ps1 / iniciar.sh / testar.ps1
├── Dockerfile / compose.yaml / .env.example
├── deploy/Caddyfile
├── docs/
│   ├── GUIA-INSTALACAO.md
│   ├── ARQUITETURA.md
│   ├── VALIDACAO.md
│   ├── exemplos/
│   └── referencia/POP_BasedeConhecimento_CSTIC.pdf
├── src/Cstic.KnowledgeBase/
│   ├── Cstic.KnowledgeBase.csproj
│   ├── Program.cs
│   ├── appsettings.json
│   ├── Models/KnowledgeArticle.cs
│   ├── Services/ArticleValidator.cs
│   ├── Services/DocumentService.cs / DocumentImportService.cs
│   ├── Pages/Index.cshtml / Editor.cshtml / Editor.cshtml.cs / Error.cshtml
│   ├── Templates/POP_BasedeConhecimento_CSTIC.docx
│   └── wwwroot/css, js, img, modelo-pop.pdf
└── tests/
    ├── Cstic.KnowledgeBase.Tests/
    └── browser_smoke.py
```

## Tecnologias

- **ASP.NET Core / Razor Pages:** servidor web e página HTML, programados em C#.
- **JavaScript e CSS locais:** campos dinâmicos, validação, responsividade e download.
- **Bibliotecas padrão do .NET:** manipulação de ZIP/XML do Word original.
- **Docker e Caddy opcionais:** implantação e HTTPS.

Não há pacotes NuGet de terceiros no aplicativo, Node/npm obrigatório, CDN,
serviço externo de geração de documentos, licença comercial ou Office no servidor.
O nome parecido com “Brave” provavelmente era **Blazor**. É uma tecnologia web do
.NET; neste caso, Razor Pages permite resolver o formulário com uma instalação
mais simples. Brave é um navegador e também pode acessar esta aplicação.

## Testar

```bash
dotnet run --project tests/Cstic.KnowledgeBase.Tests -c Release
```

São **32 verificações** de validação, geração/importação do Word, numeração, data e isolamento
entre documentos simultâneos. O executável retorna código diferente de zero se
uma verificação falhar.

Teste de navegador opcional, com o site já iniciado:

```bash
python -m pip install playwright
python -m playwright install chromium
python tests/browser_smoke.py --url http://localhost:5080
```

Esse teste executa **74 verificações funcionais** e grava resultados em
`artifacts/browser`, uma pasta ignorada pelo Git. Python/Playwright não são
necessários para executar a aplicação.

## PDF

O download principal é o Word editável, preservando o modelo enviado. Para gerar
PDF, abra o `.docx` no Word ou LibreOffice e selecione **Exportar como PDF**.
O servidor não exige conversor PDF. Em `docs/exemplos` há Word e PDF demonstrativos.

## Limites desta versão

- Até 20 ações, 100 subetapas no total, 20 detalhes por subetapa e 180.000 caracteres no POP inteiro.
- Limites de tamanho de textos e solicitação; validação também no servidor.
- Sem anexar imagens, cadastro de técnicos ou aprovação digital.
- Importação restrita a `.docx` de texto no modelo CSTIC, até 2 MB.
- O nome de aprovador no documento é um campo informado, e não uma assinatura
  nem uma confirmação eletrônica de aprovação.
- Não há salvamento automático: fechar/recarregar a página descarta o formulário.
- Sem autenticação, quem tiver acesso ao endereço pode preencher e gerar documentos.

O projeto foi adicionado como uma pasta independente em `cleitonkeliton/Testes`.
Os projetos Java/Python anteriores não são dependências deste aplicativo.
