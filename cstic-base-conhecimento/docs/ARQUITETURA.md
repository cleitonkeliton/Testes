# Arquitetura e decisões

## Fluxo

```text
GET / → escolha Criar ou Validar
  ├─ GET /criar → editor inicialmente vazio
  └─ GET /validar → upload .docx
       ↓ POST /validar?handler=Import + arquivo + token antiforgery
       ZIP/XML limitado → confere modelo → dados JSON + avisos
       ↓ editor preenchido para revisão
POST /criar?handler=Generate ou /validar?handler=Generate
  ↓ Payload JSON + token antiforgery
Validação → cópia em memória do template original → preenchimento XML
  ↓ resposta attachment → download .docx
```

## Modelo de dados

`KnowledgeArticle`: número, nome, versão, autor, aprovador, objetivo, resultado,
requisitos, executor e lista de `ProcedureAction`.

`ProcedureAction`: nome da ação e lista de `ProcedureStep`.

`ProcedureStep`: instrução e detalhes opcionais em marcadores.

Os números não vêm do navegador. O gerador calcula **1º, 2º, 3º…** para as ações
e reinicia **1.1, 1.2…** dentro de cada uma. Isso impede números duplicados/incorretos
causados por adicionar, remover ou reordenar itens. O comportamento repete 1.1 em
cada ação propositalmente, conforme solicitado.

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
Payload JSON: até 180.000 caracteres; corpo HTTP: até 2.162.688 bytes, incluindo os uploads.

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
A versão/autor/aprovador são preservados para edição; a data final é atualizada.
Um arquivo inválido não substitui a revisão carregada no navegador. Texto recebido
é mostrado com `textContent`/campos, nunca executado como HTML.

## Estado e publicação

Não há login, banco, histórico, serviço externo nem conteúdo de POP salvo no
servidor. O estado do formulário fica na aba do navegador, sem localStorage.
Em produção o ASP.NET Core pode usar chaves de proteção para cookies antiforgery;
essas chaves não são armazenamento de POPs. Reinícios podem exigir recarregar a página.

O SDK é necessário para compilar; o runtime ASP.NET Core é suficiente para
executar uma publicação dependente de runtime. Docker/IIS/Caddy são opções
de hospedagem e não dependências do formulário.

## Proteções incluídas

Token antiforgery, validação no servidor, limites de corpo/coleções, política de
conteúdo local, bloqueio de embedding em iframe, `nosniff`, respostas POST sem
cache e limite de 60 envios por minuto por IP observado. Leituras e arquivos
estáticos não consomem esse limite. Não se confia em `X-Forwarded-For` arbitrário.
Em um proxy, vários técnicos podem compartilhar o mesmo IP observado.

Essas medidas não adicionam controle de acesso: a aplicação continua sem login,
como solicitado. Um domínio público torna o formulário acessível a quem alcançar
o endereço. Restrição institucional pode ficar na rede/VPN/proxy.

## Funcionalidades fora da versão

Imagens/anexos no formulário, importação de PDF ou modelos livres, histórico, identificação via AD,
aprovação digital, numeração central sequencial e PDF automático no servidor.
A entrega principal é o `.docx` fiel ao template. Para PDF, use Word/LibreOffice.
