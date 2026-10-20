# Arquitetura e decisões

## Fluxo

```text
GET /  → sem sessão: /entrar        com sessão: /pops (técnico) ou /admin/pops (administrador)
/cadastro → conta Pendente → administrador aprova em /admin/usuarios → conta Ativa
/entrar → cookie de sessão → senha temporária? → /conta/senha (obrigatório)

Técnico:   /pops/novo ou /pops/importar → POST Save (rascunho) → POST Submit (Enviado)
Administrador: /admin/pops → /pops/{id} → POST Validate (Validado) ou Return (Devolvido)
Devolvido → técnico edita → Submit        Validado → técnico "Revisar" → rascunho da revisão

Word: GET /pops/{id}?handler=Docx[&versao=1.0]  ou  POST Download no editor
  → cópia em memória do template original → preenchimento XML → attachment .docx
```

## Modelo de dados

Banco SQLite via Entity Framework Core (`Data/AppDbContext.cs`, migrations em
`Data/Migrations`, aplicadas automaticamente ao iniciar).

- `UserAccount`: login único, nome completo, função (lista fixa), perfil
  (Técnico/Administrador), situação (Pendente/Ativo/Inativo), hash da senha, troca
  obrigatória, tentativas incorretas, fim do bloqueio e *security stamp*.
- `Pop`: número (nulo até a primeira validação), última versão validada, título,
  autor, situação (Rascunho/Enviado/Devolvido/Validado), conteúdo atual em JSON,
  comentário de devolução e datas.
- `PopEvent`: histórico — criação, importação, envio, devolução (com comentário),
  validação e início de revisão, com usuário e data.
- `PopVersion`: cópia imutável do documento exatamente como validado, com número,
  versão, aprovador e data; é a fonte do Word oficial de cada versão.

O conteúdo editável continua sendo `KnowledgeArticle` → `ProcedureAction` →
`ProcedureStep`, guardado em JSON. Nº, versão, autor e aprovador **não vêm do
navegador**: são preenchidos no servidor a partir do POP, da conta do autor e do
administrador que valida.

Os números das ações e subetapas também não vêm do navegador. O gerador calcula
**1º, 2º, 3º…** para as ações e reinicia **1.1, 1.2…** dentro de cada uma.

### Número e versão

- Primeira validação sem número: o maior número numérico existente + 1, com quatro
  dígitos (`0001`). Índice único impede repetição.
- Versão: `1.0` na primeira validação; depois o número menor sobe (`1.1`, `1.2`…).
- Importação: número do arquivo mantido se válido e livre; versão do arquivo vira a
  "última validada", então a próxima validação a incrementa.

## Preenchimento do DOCX

O template original é incorporado como recurso do assembly durante o build.
Não depende de caminhos de arquivo específicos no servidor depois de publicar.

`ZipArchive` abre o arquivo original, copia suas entradas e modifica somente:

- `word/document.xml`: conteúdo das cinco tabelas e ações/subetapas.
- `word/header*.xml`: valores das seis linhas do cabeçalho.
- `word/footer*.xml`: título no rodapé; mantém os campos `PAGE` de paginação.
- `docProps/core.xml`: autor, título e datas de geração do novo documento.
- `word/settings.xml`: solicita atualização de campos na abertura.

Estilos, relacionamentos, fontes, tema, numerações dos títulos e demais partes
do modelo são preservados. A tabela de procedimentos permite dividir linhas
entre páginas e repetir o título. Textos são inseridos como nós XML, com escaping,
e quebras de linha como `w:br`.

O documento final conserva o papel A4, o cabeçalho CSTIC, as tabelas cinzas e o
rodapé original. A quantidade de páginas cresce conforme o texto. Word e
LibreOffice podem ter pequenas diferenças de paginação/fontes; o exemplo foi
aberto e renderizado no LibreOffice para conferência.

## Validação

O navegador usa campos `required`, limites e formatos. O servidor **repete a
validação**, independentemente do que o JavaScript enviou. Listas e itens nulos,
caracteres XML inválidos e quantidades excessivas são rejeitados. JSON inválido
recebe 400; erros de campos recebem mensagens identificadas pelo caminho
`actions[0].steps[1].instruction`, por exemplo.

Limites: 20 ações, 100 subetapas somadas, 20 detalhes por subetapa, nome 160,
ação 200, subetapa 500, detalhe 2.000, blocos de texto 5.000 caracteres.
Payload JSON: até 180.000 caracteres no total, conferido no navegador, na geração e também
na importação (um POP que não poderia ser gerado é recusado ao carregar); corpo HTTP: até 2.162.688 bytes, incluindo os uploads.

O nome do arquivo é normalizado, sem caminhos e limitado em tamanho.
A geração de documentos é independente por solicitação; o template original
fica somente para leitura em memória. Há um teste de documentos simultâneos.

## Importação de base existente

`DocumentImportService` lê `.docx` de até 2 MB via `ZipArchive`, sem extrair arquivos
ou executar conteúdo. Limita a 200 entradas e 12 MB descomprimidos, rejeita nomes
duplicados/macros e usa `XmlReader` com DTD proibida e `XmlResolver = null`.
Relacionamentos externos não são carregados. O limite de memória para multipart
mantém uploads aceitos em memória; não há persistência dos documentos.

Confere as cinco tabelas/seções e as seis linhas do cabeçalho ativo, usando os
relacionamentos OOXML. Preserva quebras de linha, acentos, ações `1º`/`2º`, subetapas
`1.1`/`1.2` e detalhes em marcadores. Numeração automática antiga é convertida
em uma ação genérica com aviso, para conferência humana. Campos vazios/formatos
inválidos podem ser carregados para corrigir; a geração exige dados válidos.
Textos/coleções maiores que os limites são rejeitados, sem truncamento.

O formulário importa conteúdo textual. Imagens, objetos, tabelas adicionais e
alterações controladas são rejeitados para evitar perda silenciosa. A geração
sempre usa os estilos do template original, não os estilos do arquivo recebido.
Um arquivo válido vira um POP em rascunho do técnico que importou (evento
"Importado" no histórico); número e versão seguem as regras acima, e o autor passa
a ser esse técnico. Um arquivo inválido não cria nada. Texto recebido é mostrado
com `textContent`/campos, nunca executado como HTML.

## Contas e sessão

- Cookie de autenticação do ASP.NET Core (`HttpOnly`, `SameSite=Lax`), 8 horas com
  renovação por uso. Chaves de proteção persistidas em `App_Data/chaves`, então
  reinícios não derrubam sessões.
- A cada requisição a conta é recarregada do banco: conta desativada, senha
  redefinida ou perfil alterado (troca do *security stamp*) encerra a sessão na hora.
- Senhas com `PasswordHasher` (PBKDF2). Login inexistente também passa por uma
  verificação de hash, para o tempo de resposta não revelar quais logins existem;
  a situação do cadastro (pendente/desativado) só aparece para quem acertou a senha.
- Cinco senhas incorretas bloqueiam a conta por 15 minutos.
- Senha temporária (administrador inicial, criação ou redefinição pelo administrador)
  bloqueia todas as páginas, exceto `/conta/senha` e `/sair`, até ser trocada.
- Sempre resta ao menos um administrador ativo; ninguém remove o próprio acesso.
- O administrador inicial é criado de `appsettings.json` somente quando não existe
  nenhum administrador. Recuperação sem administrador: comando
  `redefinir-senha <login>`, executado no servidor, que imprime uma senha temporária.

## Autorização

- Política padrão: toda página exige login, exceto `/entrar`, `/cadastro`, `/Error`
  e `/health`.
- Pasta `Pages/Admin`: somente perfil Administrador.
- POPs: o técnico vê e edita apenas os próprios; o administrador vê todos. Edição só
  em Rascunho, Devolvido ou Validado (que inicia revisão); nunca enquanto Enviado.
- Validar/devolver só para administradores e só para POPs em Enviado.
- Requisições do JavaScript enviam o cabeçalho `X-Cstic-Fetch` e recebem 401/403
  em vez de redirecionamento; o formulário avisa e mantém os campos preenchidos.

## Proteções incluídas

Token antiforgery, validação no servidor, limites de corpo/coleções, política de
conteúdo local, bloqueio de embedding em iframe, `nosniff`, respostas POST sem
cache e limite de 60 envios por minuto por IP observado. Leituras e arquivos
estáticos não consomem esse limite. O conteúdo inicial do editor vai numa tag
`<script type="application/json">` com `<` escapado. Textos do usuário são exibidos
como texto, nunca interpretados como HTML.

## Estado e publicação

POPs, usuários e histórico ficam em `App_Data/cstic.db` (configurável em
`Dados:Pasta`). Essa pasta precisa de gravação pelo usuário do site e deve entrar no
backup. O estado do formulário fica na aba até ser salvo; não há localStorage. Sair
da página com alterações não salvas pede confirmação do navegador.

O SDK é necessário para compilar; o runtime ASP.NET Core é suficiente para
executar uma publicação dependente de runtime. Docker/IIS/Caddy são opções
de hospedagem. No Docker, `App_Data` é um volume.

## Funcionalidades fora da versão

Avisos por e-mail, biblioteca de POPs validados para consulta de toda a equipe,
imagens/anexos, importação de PDF ou modelos livres, identificação via AD,
assinatura digital certificada e PDF automático no servidor.
