# Guia completo: instalar e colocar o site para funcionar

## 1. Entenda o que está sendo entregue

Você terá um **site**, não um programa de formulários que cada técnico precisa
instalar. Um computador/servidor executa a aplicação. Os técnicos acessam o
endereço no navegador — Edge, Chrome, Firefox ou Brave.

Cada técnico entra com login e senha. Os POPs ficam salvos no servidor, num banco
**SQLite** (um único arquivo), e seguem o fluxo **rascunho → enviado → devolvido ou
validado**. O administrador valida ou devolve cada POP, aprova cadastros e gerencia
usuários. O Word é gerado a partir de uma cópia do modelo original.

**Pasta de dados:** o banco (`cstic.db`) e as chaves das sessões (`chaves\`) ficam
na pasta `App_Data`, dentro da pasta do site. Ela é criada na primeira execução,
precisa de **permissão de gravação** e deve entrar no **backup** (seção 9).
Para usar outro local, defina a configuração `Dados:Pasta` (por exemplo, a variável
de ambiente `Dados__Pasta=D:\DadosCsticPop`).

**Escolha o caminho de implantação:**

| Situação | Caminho indicado |
|---|---|
| Quero testar no meu computador Windows | Seção 2 |
| Quero abrir em outros computadores do hospital | Seção 4 |
| Já tenho servidor Windows com IIS | Seção 5 |
| Quero executar com Docker | Seção 6 |
| Quero um endereço HTTPS online | Seção 7 |

## 2. Primeiro teste no Windows, passo a passo

### 2.1 Instale o .NET correto

1. Entre em https://dotnet.microsoft.com/pt-br/download/dotnet/10.0.
2. Na coluna **SDK**, baixe o instalador **Windows x64**, se seu Windows for x64.
   Em computador ARM64, escolha ARM64.
3. Instale normalmente. Para executar/compilar o código fonte, precisa do
   **SDK**, e não apenas do runtime.
4. Feche e abra novamente o PowerShell ou Terminal do Windows.
5. Execute:

```powershell
dotnet --info
```

O resultado deve listar um SDK **10.0.x**. Não é necessário instalar Visual
Studio, Node.js, Python, servidor de banco de dados ou Word no computador que hospeda o site.
Para editar o código, pode usar VS Code ou Visual Studio com suporte ao .NET 10.

### 2.2 Extraia o projeto

1. Baixe o ZIP entregue.
2. Clique com o botão direito e escolha **Extrair tudo**.
3. Como exemplo, coloque a pasta em:

```text
C:\Projetos\cstic-base-conhecimento
```

A pasta correta é a que contém **README.md**, **global.json** e a pasta **src**.
Não tente executar a aplicação dentro do ZIP.

### 2.3 Abra o terminal na pasta

No Explorador de Arquivos, entre na pasta, clique na barra de endereços,
digite `powershell` e pressione Enter. Ou abra o PowerShell e execute:

```powershell
cd "C:\Projetos\cstic-base-conhecimento"
```

### 2.4 Inicie o site

```powershell
dotnet run --project src/Cstic.KnowledgeBase -c Release --urls http://localhost:5080
```

Na primeira execução, o .NET prepara e compila o projeto. Quando aparecer
`Now listening on: http://localhost:5080`, o site estará aguardando conexões.
**Deixe esse terminal aberto.** Fechar o terminal ou pressionar `Ctrl+C` para o site.

Também há um atalho:

```powershell
powershell -ExecutionPolicy Bypass -File .\iniciar.ps1
```

Esse parâmetro libera o script apenas naquele processo do PowerShell; não muda
a política permanente da máquina. Se a política institucional impedir scripts,
use o comando `dotnet run` diretamente.

### 2.5 Abra o navegador

Digite na barra de endereços:

```text
http://localhost:5080
```

`localhost` significa **o próprio computador**. Neste modo, um colega em outra
máquina ainda não acessa o site; a seção 4 explica a rede local.

### 2.6 Primeiro acesso do administrador

Na primeira execução, o site cria o administrador:

| Login | Senha inicial |
|---|---|
| `adm.audi` | `Senha@123456` |

1. Entre com esses dados. O site exige **trocar a senha** antes de liberar o painel.
2. Escolha uma senha com pelo menos 10 caracteres, letra e número, sem conter o login.
3. Em **Usuários → Editar dados**, troque o nome "Administrador CSTIC" pelo seu nome
   completo. **Esse nome é impresso como Aprovador** nos POPs que você validar.

A senha inicial vem de `appsettings.json` (`AdministradorInicial`) e só é usada se o
banco ainda não tiver nenhum administrador. Depois da troca, ela deixa de valer.

### 2.7 Cadastro e aprovação dos técnicos

1. Na tela de login, o técnico clica em **Solicitar cadastro** e informa nome completo,
   função (lista fixa), login no formato `primeironome.ultimonome`, senha e confirmação.
   Letras maiúsculas e acentos do login são convertidos: `José.Souza` vira `jose.souza`.
   Para nomes repetidos, acrescenta-se um número: `maria.silva2`.
2. O cadastro fica **aguardando aprovação**; o técnico ainda não consegue entrar.
3. O administrador abre **Usuários**, confere nome e função e clica em **Aprovar**,
   escolhendo a permissão (Técnico ou Administrador), ou em **Recusar**.

Em **Usuários**, o administrador também pode:

- **Criar novo cadastro** já ativo, com permissão de técnico ou administrador. A pessoa
  troca a senha temporária no primeiro acesso.
- **Redefinir senha** de outra pessoa. A senha atual nunca é mostrada; a nova é
  temporária e a troca é exigida no próximo login. As sessões abertas são encerradas.
- **Editar dados** (nome, função e permissão) e **Desativar/Reativar** acessos.
  Desativar mantém os POPs e o histórico. O sistema não deixa remover o último
  administrador ativo nem o seu próprio acesso.

Cinco senhas incorretas seguidas bloqueiam o login por 15 minutos. A sessão expira
após 8 horas sem uso; use **Sair** em computadores compartilhados.

### 2.8 Criar e enviar um POP (técnico)

1. Em **Criar POP**, clique em **Preencher um exemplo** para conhecer o funcionamento.
2. Preencha nome, executor, **Objetivo, Resultado, Requisitos** e o procedimento.
   **Nº, versão, autor, aprovador e data são automáticos.**
3. Em cada ação, as subetapas começam em **1.1**. Use `↑`, `↓` e `×` para organizar.
4. Clique em **Salvar rascunho** quando quiser; o POP aparece em **Meus POPs**.
5. Clique em **Enviar para validação**. Faltando informação, o rascunho é salvo e os
   campos a corrigir são indicados.
6. Enquanto aguarda validação, o POP não pode ser alterado.
7. Se o administrador **devolver**, o motivo aparece em **Meus POPs** e no editor.
   Corrija e envie novamente.
8. Depois de **validado**, o POP recebe número e versão. Para atualizá-lo, clique em
   **Revisar**: a versão validada continua disponível até a nova ser aprovada.

**Baixar Word deste conteúdo** gera um arquivo a qualquer momento, mas antes da
validação o cabeçalho traz `PENDENTE` no número e "Pendente de validação" no aprovador.
O documento oficial é o baixado depois da validação.

### 2.9 Validar ou devolver (administrador)

1. Em **POPs recebidos**, a aba **Aguardando validação** lista os POPs com o nome e a
   função de cada técnico. O menu mostra quantos estão pendentes.
2. Clique em **Analisar** para ver o conteúdo, no mesmo formato do documento, e o histórico.
3. **Validar POP:** o POP recebe o próximo número livre (`0001`, `0002`…) na primeira
   validação; a versão começa em **1.0** e sobe a cada revisão validada (**1.1**, **1.2**…).
   Você fica como aprovador e a data é a da validação.
4. **Devolver ao técnico:** descreva a inconsistência; o comentário é obrigatório.
5. As abas **Devolvidos**, **Validados**, **Rascunhos** e **Todos** ajudam a acompanhar.
   Cada versão validada pode ser baixada pela página do POP.

### 2.10 Importar um POP existente

1. Em **Importar Word**, selecione um **`.docx` de até 2 MB** no modelo CSTIC
   (cabeçalho de identificação e cinco seções em tabelas). PDF e `.doc` não são aceitos.
2. Clique em **Carregar para revisão**. O arquivo vira um **rascunho seu**, já aberto
   no editor, e segue o mesmo fluxo de envio e validação.
3. O número do arquivo é mantido se ainda não for usado por outro POP; caso contrário,
   o número é atribuído na validação. A versão do arquivo é a base: um POP 2.3
   importado é validado como 2.4.
4. Leia os avisos. Um documento antigo sem ações `1º`, `2º`… terá suas instruções
   agrupadas em **Procedimento importado**; reorganize antes de enviar.

**Se a importação falhar:** o aviso indica formato, estrutura ou tamanho inválido.
Não basta renomear PDF para `.docx`. Para um `.doc` antigo, abra no Word/LibreOffice
e salve como `.docx`, mantendo o modelo. Documentos com imagens/objetos ou alterações
controladas não são aceitos: faça uma cópia somente com texto e finalize as alterações
antes de importar. Fontes, cores e outros ajustes do Word recebido são substituídos
pelo formato padronizado do template. Textos acima dos limites são rejeitados, sem
cortar conteúdo.

## 3. Transformar o Word em PDF

A aplicação entrega **Word editável**, escolhida para preservar o arquivo
original e manter o servidor simples.

**No Microsoft Word:**

1. Abra o `.docx`.
2. Escolha **Arquivo → Exportar → Criar documento PDF/XPS**, ou
   **Salvar como → PDF**, conforme sua versão do Word.
3. Escolha uma pasta e salve.

**No LibreOffice Writer:**

1. Abra o `.docx`.
2. Escolha **Arquivo → Exportar como → Exportar como PDF**.
3. Confira as opções e salve.

O LibreOffice é gratuito: https://www.libreoffice.org/download/download-libreoffice/.
A conversão não é necessária para preencher o site nem para baixar o Word.

## 4. Usar na rede local do hospital

Escolha uma máquina que ficará ligada e será o **servidor**. Os demais técnicos
precisam apenas de navegador e acesso à mesma rede/VPN institucional.

### 4.1 Descubra o IP do servidor

No servidor Windows:

```powershell
ipconfig
```

Procure **Endereço IPv4** do adaptador da rede utilizada. Como exemplo, vamos
supor `192.168.1.50`. Esse número é ilustrativo; use o IP real.

### 4.2 Escute na interface de rede

Na pasta do projeto, execute:

```powershell
dotnet run --project src/Cstic.KnowledgeBase -c Release --urls http://0.0.0.0:5080
```

Ou:

```powershell
powershell -ExecutionPolicy Bypass -File .\iniciar.ps1 -Rede
```

`0.0.0.0` faz o servidor aceitar conexões pelas interfaces de rede. Esse **não**
é o endereço que o técnico digita no navegador.

### 4.3 Libere a porta no firewall

Se necessário, abra PowerShell como administrador no servidor e crie a regra
somente para os perfis de rede institucional/domínio e privada:

```powershell
New-NetFirewallRule -DisplayName "CSTIC POP 5080" -Direction Inbound -Action Allow -Protocol TCP -LocalPort 5080 -Profile Domain,Private
```

Se o firewall ou a rede for gerenciado centralmente, solicite à equipe responsável
a liberação da porta entre as redes necessárias.

### 4.4 Abra em outra máquina

No exemplo do IP acima:

```text
http://192.168.1.50:5080
```

O computador que hospeda precisa permanecer ligado e a aplicação precisa estar
executando. Para não depender de um terminal aberto, use IIS (seção 5) ou Docker
com reinício automático (seção 6).

Para um nome amigável, a equipe de infraestrutura pode criar um registro DNS
interno, como `pop.hc.exemplo`, apontando para o servidor. Para HTTPS na intranet,
use um certificado institucional e o IIS ou o proxy já usado pelo hospital.

## 5. Hospedar em Windows/IIS

Este caminho permite executar o site como serviço gerenciado pelo IIS.

1. No servidor, habilite **Internet Information Services (IIS)** pelos recursos
   do Windows ou Gerenciador do Servidor. Inclua a console de gerenciamento.
2. Depois de instalar o IIS, instale o **ASP.NET Core Hosting Bundle do .NET 10**,
   disponível na página de downloads do .NET 10. Ele é diferente do SDK e inclui
   o módulo de integração com IIS.
3. Reinicie o IIS/servidor conforme a janela de manutenção utilizada pela equipe.
4. Na máquina de desenvolvimento, publique:

```powershell
dotnet publish src/Cstic.KnowledgeBase -c Release -o .\publicacao
```

5. Copie o **conteúdo** da pasta `publicacao` para uma pasta do servidor, por exemplo:
   `C:\Sites\CsticPop`. O publish também gera `web.config` para o IIS.
6. No Gerenciador do IIS, crie um **Pool de Aplicativos**, por exemplo `CsticPop`.
   Configure **Versão do .NET CLR → Sem código gerenciado**. O site usa .NET moderno,
   e não o .NET Framework antigo do IIS.
7. Adicione um site com caminho físico `C:\Sites\CsticPop` e associe o pool.
8. Configure o binding: porta, nome DNS e certificado HTTPS, conforme a infraestrutura.
9. Dê permissão de **leitura e execução** da pasta ao usuário do pool,
   `IIS AppPool\CsticPop`.
10. Crie a pasta `C:\Sites\CsticPop\App_Data` e dê a `IIS AppPool\CsticPop`
    permissão de **Modificar** nela. É ali que ficam o banco e as chaves de sessão.
    No PowerShell como administrador:

    ```powershell
    New-Item -ItemType Directory -Force C:\Sites\CsticPop\App_Data
    icacls C:\Sites\CsticPop\App_Data /grant "IIS AppPool\CsticPop:(OI)(CI)M"
    ```

11. Mantenha **Autenticação Anônima** habilitada no IIS: o login é feito pelo
    próprio site, não pelo Windows.
12. Use **HTTPS** no binding, já que o site agora trafega senhas.
13. Acesse o endereço configurado e faça o primeiro acesso do administrador (seção 2.6).

**Atualizar a versão no IIS:** pare o site ou o pool, copie o conteúdo da nova
publicação **sem apagar `App_Data`** e inicie novamente. Ao iniciar, o site aplica
automaticamente as mudanças de estrutura do banco, preservando os dados.

Se publicar em um Windows diferente, compile usando `dotnet publish` nesse
ambiente ou publique explicitamente para o destino, por exemplo:

```powershell
dotnet publish src/Cstic.KnowledgeBase -c Release -r win-x64 --self-contained false -o .\publicacao-windows
```

O servidor precisa do Hosting Bundle .NET 10 instalado. O pacote de código fonte
entregue permite essa publicação; a validação desta entrega foi feita em Linux,
e não em um servidor IIS real.

## 6. Executar com Docker

Instale Docker Desktop no computador de teste ou Docker Engine no servidor.
Os comandos abaixo devem ser executados na pasta do projeto.

### 6.1 Criar a imagem

```bash
docker build -t cstic-pop:1.0 .
```

O Dockerfile compila no SDK oficial .NET 10 e entrega uma imagem com o runtime
ASP.NET Core. O container executa com usuário sem privilégios.

### 6.2 Iniciar localmente

```bash
docker run -d --name cstic-pop --restart unless-stopped -p 127.0.0.1:5080:8080 -v cstic_dados:/app/App_Data cstic-pop:1.0
```

Abra `http://localhost:5080`.

Para permitir rede local, use o mapeamento sem o endereço de loopback:

```bash
docker run -d --name cstic-pop --restart unless-stopped -p 5080:8080 -v cstic_dados:/app/App_Data cstic-pop:1.0
```

Use uma das duas opções; não crie dois containers com o mesmo nome. **Os usuários e
POPs ficam no volume `cstic_dados`**, não no container: recriar o container mantém os
dados, mas remover o volume apaga tudo.

### 6.3 Operação

```bash
docker logs cstic-pop
docker stop cstic-pop
docker start cstic-pop
```

`--restart unless-stopped` reinicia o container após reinício do Docker, desde
que ele não tenha sido parado manualmente. No Docker Desktop, configure também
o início do Docker ao ligar/iniciar sessão no computador.

## 7. Colocar online com HTTPS

Use um servidor com Docker e um domínio real. O `compose.yaml` inclui **Caddy**,
que serve HTTPS e encaminha as solicitações ao aplicativo.

### 7.1 Prepare servidor e domínio

1. Disponibilize uma VM/servidor Linux com Docker Engine e plugin Docker Compose.
2. Tenha um domínio/subdomínio autorizado para o serviço, por exemplo
   `pop.seu-dominio.org.br`.
3. Crie um registro DNS **A** apontando esse nome para o IP público do servidor.
   Se houver registro AAAA, ele também precisa apontar para o servidor correto.
4. Libere portas **80 e 443** no firewall e, se houver, no roteador/security group.
5. Copie a pasta do projeto para o servidor.

O domínio e o IP de exemplo são substitutos; esta entrega não registra domínio
nem publica automaticamente num servidor externo.

### 7.2 Configure o domínio

Na raiz do projeto, copie `.env.example` para `.env`:

Linux:

```bash
cp .env.example .env
```

PowerShell:

```powershell
Copy-Item .env.example .env
```

Abra `.env` em um editor e substitua o valor:

```dotenv
DOMAIN=pop.seu-dominio.org.br
```

Não use `http://`, `https://`, porta ou barra nesse valor. O arquivo não contém
senha; só informa o nome do site ao Caddy.

### 7.3 Inicie

```bash
docker compose up -d --build
```

O Caddy solicita o certificado automaticamente quando DNS e conectividade
estiverem corretos. Aguarde a inicialização e consulte:

```bash
docker compose ps
docker compose logs --tail 100 app caddy
```

Depois abra:

```text
https://pop.seu-dominio.org.br
```

A porta 8080 do aplicativo permanece apenas na rede interna do Compose. O volume
`app_dados` guarda o banco com usuários e POPs; `caddy_data` e `caddy_config` retêm
os certificados do proxy. Não remova esses volumes.

### 7.4 Atualizar o site

Copie o código atualizado e execute novamente:

```bash
docker compose up -d --build
```

Para parar os serviços sem remover os volumes:

```bash
docker compose down
```

Qualquer pessoa que alcance o endereço vê a tela de login e pode **solicitar**
cadastro, mas só entra depois da aprovação do administrador. Se o site for publicado
na internet, considere restringir o acesso à rede/VPN institucional. A validação
registra no histórico quem aprovou e quando; não é uma assinatura digital certificada.

## 8. Linux/macOS sem Docker

Instale o SDK .NET 10 compatível com o sistema e a arquitetura. Na pasta do projeto:

```bash
bash iniciar.sh
```

Para outro endereço/porta:

```bash
ENDERECO=0.0.0.0 PORTA=5080 bash iniciar.sh
```

Para publicar uma versão compilada:

```bash
dotnet publish src/Cstic.KnowledgeBase -c Release -o publicacao
cd publicacao
dotnet Cstic.KnowledgeBase.dll --urls http://0.0.0.0:5080
```

O banco fica em `publicacao/App_Data`. Ao substituir a publicação, preserve essa pasta.

O runtime **ASP.NET Core 10** precisa estar instalado. Essa publicação depende
do runtime; não é um executável autocontido. Mantenha o diretório como diretório
de trabalho, para que os arquivos de CSS, JavaScript e imagens sejam encontrados.

## 9. Backup e recuperação de acesso

**Backup:** copie a pasta `App_Data` inteira (banco `cstic.db` e pasta `chaves`).
Para uma cópia consistente, pare o site/pool antes de copiar ou copie fora do horário
de uso. Para restaurar, pare o site, devolva a pasta e inicie novamente.

**Esqueci a senha do administrador:** outro administrador pode redefini-la em
**Usuários**. Se não houver outro, execute no servidor, na pasta da publicação:

```powershell
dotnet Cstic.KnowledgeBase.dll redefinir-senha adm.audi
```

O comando reativa a conta, mostra uma **senha temporária** e exige a troca no próximo
login. Só funciona para quem tem acesso ao servidor. Sem a pasta publicada, use
`dotnet run --project src/Cstic.KnowledgeBase -c Release -- redefinir-senha adm.audi`.
Se a pasta de dados não for a padrão, defina `Dados__Pasta` antes de executar.

## 10. Diagnóstico de problemas

| Problema | Como resolver |
|---|---|
| `dotnet` não é reconhecido | Instale o SDK .NET 10, feche/abra o terminal e execute `dotnet --info`. |
| SDK incompatível/global.json | Use SDK 10.0.x. A opção `latestFeature` permite versões de manutenção/features do .NET 10. |
| Projeto não encontrado | Entre na pasta que contém `src` e `README.md`; confira o caminho do comando. |
| Porta já em uso | Pare a instância anterior ou use `--urls http://localhost:5081`; abra a mesma porta no navegador. |
| Abre no servidor, mas não na rede | Use `0.0.0.0`, o IP real do servidor, a porta certa e confira firewall/rota/VPN. |
| Página sem estilo na publicação | Execute a DLL de dentro da pasta publicada e preserve a pasta `wwwroot`. |
| Não gera o Word | Preencha os campos obrigatórios e todas as ações/subetapas; veja a mensagem no formulário. |
| Mensagem de formulário expirado | A sessão pode ter expirado. Entre novamente em outra aba e tente de novo; os campos continuam preenchidos. Salve rascunhos com frequência. |
| Erro 500 ao iniciar / banco somente leitura | O usuário do site precisa de permissão de gravação em `App_Data` (seção 5, passo 10). |
| Todos foram desconectados após atualizar | A pasta `App_Data\chaves` foi apagada ou trocada. Preserve `App_Data` nas atualizações. |
| Login bloqueado | Após cinco senhas incorretas, aguarde 15 minutos ou peça ao administrador para redefinir a senha. |
| Muitas solicitações | Há limite de 60 envios por minuto por IP observado. Aguarde um minuto. Atrás de proxy os usuários podem compartilhar esse IP. |
| Download não aparece | Confira o aviso de download e a pasta Downloads do navegador. |
| Certificado online não é emitido | Confira domínio real, DNS A/AAAA, portas 80/443 e logs do Caddy. |
| IIS retorna 500.30/502.5 | Confira Hosting Bundle .NET 10, pool, permissões e logs do IIS; execute a DLL em terminal para identificar o erro. |

Endpoint de verificação operacional:

```text
/health
```

Ele retorna `{"status":"ok"}`. Para conferir de fato a geração de documento,
preencha um exemplo e baixe o Word; a resposta do health sozinha não prova esse fluxo.

## 11. Manutenção e mudanças futuras

- Layout do site: `wwwroot/css/site.css`.
- Login, cadastro e senha: `Pages/Entrar`, `Pages/Cadastro`, `Pages/Conta/Senha`.
- Menu e estrutura das páginas: `Pages/Shared/_Layout.cshtml`.
- Editor de POP: `Pages/Pops/Editor.cshtml`; lista e detalhes: `Pages/Pops`.
- Painel do administrador: `Pages/Admin/Pops.cshtml` e `Pages/Admin/Usuarios.cshtml`.
- Ações/subetapas dinâmicas, rascunho, envio e download: `wwwroot/js/form.js`.
- Regras de conta: `Services/AccountService.cs`; fluxo dos POPs: `Services/PopService.cs`.
- Banco: `Data/AppDbContext.cs` e `Data/Migrations` (EF Core com SQLite).
- Dados e regras do documento: `Models/KnowledgeArticle.cs` e `Services/ArticleValidator.cs`.
- Funções da lista do cadastro: `UserAccount.Functions` em `Models/UserAccount.cs`.
- Preenchimento do Word: `Services/DocumentService.cs`.
- Leitura de bases existentes: `Services/DocumentImportService.cs`.
- Documento original: `Templates/POP_BasedeConhecimento_CSTIC.docx`.

Alterar textos/fontes/cores no Word original mantendo a estrutura das tabelas
costuma exigir apenas atualizar o template e recompilar. Se adicionar/remover
linhas no cabeçalho ou tabelas do corpo, adapte `DocumentService`, `DocumentImportService` e os testes:
o serviço confere as seis linhas e cinco tabelas esperadas, em vez de gerar um
documento incorreto silenciosamente.

Após uma mudança de código:

```bash
dotnet run --project tests/Cstic.KnowledgeBase.Tests -c Release
dotnet publish src/Cstic.KnowledgeBase -c Release -o publicacao
```

Se mudar as classes de `Models` que vão para o banco, gere uma migration
(`dotnet ef migrations add NomeDaMudanca --project src/Cstic.KnowledgeBase --output-dir Data/Migrations`).
Ela é aplicada automaticamente na próxima inicialização do site.
