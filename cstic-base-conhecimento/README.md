# CSTIC · Base de conhecimento

Aplicação web em **C# e ASP.NET Core 10**, com Razor Pages, para que os técnicos
da CSTIC criem Procedimentos Operacionais Padrão (POP) e os encaminhem para
**validação do administrador**. O documento final é um **Word editável `.docx`**
gerado a partir do **arquivo modelo original da CSTIC**, incorporado na aplicação.

- **Login** para técnicos e administradores. O cadastro é solicitado na tela de login
  e só é ativado após aprovação de um administrador.
- **Fluxo do POP:** rascunho → enviado para validação → devolvido com comentário ou
  validado. Número e versão são automáticos.
- **Painel do administrador:** POPs recebidos com o nome de cada técnico, validação
  ou devolução, aprovação de cadastros, criação de usuários, redefinição de senha.
- Dados guardados num banco **SQLite** (um arquivo), sem servidor de banco.

## Comece aqui

1. Instale o **SDK .NET 10**: https://dotnet.microsoft.com/pt-br/download/dotnet/10.0
2. Abra um terminal na pasta deste projeto.
3. Execute:

```bash
dotnet run --project src/Cstic.KnowledgeBase -c Release --urls http://localhost:5080
```

4. Abra `http://localhost:5080` e entre com o administrador inicial:
   login **`adm.audi`**, senha **`Senha@123456`**.
5. O site exige trocar a senha no primeiro acesso. Depois, em **Usuários → Editar
   dados**, informe seu nome completo: ele é impresso como aprovador nos POPs validados.

No Windows, também pode executar `powershell -ExecutionPolicy Bypass -File .\iniciar.ps1`
(a liberação vale apenas para esse processo). No Linux/macOS: `bash iniciar.sh`.

**Guia completo para iniciantes, rede local, IIS, Docker, backup e publicação online:**
[docs/GUIA-INSTALACAO.md](docs/GUIA-INSTALACAO.md).

## Perfis

| | Técnico | Administrador |
|---|---|---|
| Criar, salvar, importar e enviar POPs | ✓ | ✓ |
| Ver e baixar os próprios POPs | ✓ | ✓ |
| Ver todos os POPs, validar ou devolver | | ✓ |
| Aprovar/recusar cadastros, criar usuários | | ✓ |
| Redefinir senha (sem ver a atual), editar, desativar | | ✓ |

**Cadastro:** nome completo, função (lista fixa: Técnico N1, Técnico N2, Supervisor,
Qualidade, Outro), login `primeironome.ultimonome`, senha e confirmação. Senhas têm
pelo menos 10 caracteres, com letra e número, e são guardadas apenas como hash.
Senhas temporárias (primeiro acesso, criação ou redefinição pelo administrador)
precisam ser trocadas no login seguinte.

## Fluxo do POP

1. O técnico cria o POP ou importa um Word no modelo CSTIC e **salva o rascunho**.
2. **Enviar para validação** confere todos os campos; o POP fica bloqueado para edição.
3. O administrador abre o POP em **POPs recebidos** e:
   - **devolve**, com comentário obrigatório, que o técnico vê na lista e no editor; ou
   - **valida**: na primeira validação o POP recebe o próximo número (`0001`, `0002`…)
     e a versão **1.0**; o administrador fica como aprovador e a data é a da validação.
4. Para atualizar um POP validado, o técnico clica em **Revisar**. A versão validada
   continua disponível; a revisão validada sobe a versão (**1.1**, **1.2**…).

Cada POP guarda o histórico (criação, envios, devoluções, validações) e todas as
versões validadas, que podem ser baixadas. Um POP importado mantém o número do
arquivo se ele estiver livre, e a versão do arquivo é a base da próxima.

## Documento e numeração

Cabeçalho: Nº, Nome, Versão, Data criação/atualização, Autor e Aprovador.
Blocos: **1. Objetivo, 2. Resultado, 3. Requisitos, 4. Executor, 5. Procedimento**.

Dentro de **5. Procedimento**, cada ação recebe uma ordem, e **a numeração das
subetapas recomeça em 1.1 dentro de cada ação**:

```text
1º Abrir computador
   1.1 Retirar os parafusos
   1.2 Retirar a tampa

2º Limpeza
   1.1 Retirar componentes
   1.2 Realizar a limpeza
```

Antes da validação, o Word baixado traz `PENDENTE` no número e "Pendente de
validação" no aprovador. A data usa o horário de Recife (UTC−3).

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
│   ├── Program.cs                  autenticação, banco e configuração
│   ├── appsettings.json            pasta de dados e administrador inicial
│   ├── Data/                       AppDbContext e migrations do banco
│   ├── Models/                     KnowledgeArticle, UserAccount, Pop
│   ├── Services/                   contas, fluxo do POP, geração e importação do Word
│   ├── Pages/                      Entrar, Cadastro, Conta, Pops, Admin
│   ├── Templates/POP_BasedeConhecimento_CSTIC.docx
│   └── wwwroot/css, js, img, modelo-pop.pdf
└── tests/
    ├── Cstic.KnowledgeBase.Tests/
    └── browser_smoke.py
```

O banco (`cstic.db`) e as chaves de sessão ficam em `App_Data`, criada na primeira
execução. Essa pasta precisa de permissão de gravação, entra no backup e é ignorada
pelo Git.

## Tecnologias

- **ASP.NET Core / Razor Pages:** servidor web e páginas HTML, programados em C#.
- **Autenticação por cookie** do ASP.NET Core e hash de senha PBKDF2 (`PasswordHasher`).
- **Entity Framework Core com SQLite:** banco em arquivo; a estrutura é criada e
  atualizada automaticamente ao iniciar.
- **JavaScript e CSS locais:** campos dinâmicos, validação, responsividade e download.
- **Bibliotecas padrão do .NET:** manipulação de ZIP/XML do Word original.
- **Docker e Caddy opcionais:** implantação e HTTPS.

Os únicos pacotes NuGet são os do EF Core para SQLite, da Microsoft. Não há
Node/npm obrigatório, CDN, serviço externo, licença comercial ou Office no servidor.

## Testar

```bash
dotnet run --project tests/Cstic.KnowledgeBase.Tests -c Release
```

São **46 verificações** de contas (cadastro, aprovação, bloqueio, redefinição de senha,
último administrador), fluxo do POP (envio, devolução, validação, número, versão,
revisão, importação, permissões), validação e geração/importação do Word.

Teste de navegador opcional, com o site iniciado sobre uma **pasta de dados vazia**:

```bash
python -m pip install playwright
python -m playwright install chromium
Dados__Pasta=/tmp/cstic-teste dotnet run --project src/Cstic.KnowledgeBase -c Release --urls http://localhost:5080
python tests/browser_smoke.py --url http://localhost:5080
```

Esse teste executa **101 verificações funcionais** do fluxo completo e grava capturas
em `artifacts/browser`, uma pasta ignorada pelo Git. Python/Playwright não são
necessários para executar a aplicação.

## PDF

O download é o Word editável, preservando o modelo. Para gerar PDF, abra o `.docx`
no Word ou LibreOffice e selecione **Exportar como PDF**.

## Limites desta versão

- Até 20 ações, 100 subetapas no total, 20 detalhes por subetapa e 180.000 caracteres no POP inteiro.
- Importação restrita a `.docx` de texto no modelo CSTIC, até 2 MB.
- Sem anexar imagens, sem avisos por e-mail (o menu mostra contadores de pendências).
- A validação registra quem aprovou e quando; não é assinatura digital certificada.
- Técnicos veem apenas os próprios POPs; ainda não há biblioteca de POPs validados
  para consulta de toda a equipe.
