# Guia completo: instalar e colocar o site para funcionar

## 1. Entenda o que está sendo entregue

Você terá um **site**, não um programa de formulários que cada técnico precisa
instalar. Um computador/servidor executa a aplicação. Os técnicos acessam o
endereço no navegador — Edge, Chrome, Firefox ou Brave.

O servidor recebe o formulário, valida os campos e preenche uma cópia do modelo
Word original. O navegador recebe o `.docx` para download. A geração ocorre em
memória; não existe banco, conta de usuário, histórico ou armazenamento de POPs
no servidor nesta versão.

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
Studio, Node.js, Python, banco de dados ou Word no computador que hospeda o site.
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

### 2.6 Faça o primeiro documento

1. Na tela inicial, escolha **Criar base de conhecimento** e clique em **Preencher um exemplo** para conhecer o funcionamento.
2. Confira número, nome, versão, autor e aprovador.
3. A data é automática. Não precisa digitá-la.
4. Confira **Objetivo, Resultado, Requisitos e Executor**.
5. Veja as quatro ações do exemplo. Em cada ação as subetapas começam em 1.1.
6. Clique em **Gerar documento Word**.
7. O navegador iniciará o download. Se estiver configurado para perguntar,
   escolha onde salvar. Caso contrário, confira a pasta **Downloads**.
8. Abra o arquivo no Word ou LibreOffice Writer.

O arquivo de exemplo serve para demonstrar o preenchimento; revise o conteúdo
técnico e substitua os dados pelo procedimento real antes de utilizá-lo.

### 2.7 Gere seu próprio POP

1. Clique em **Limpar formulário** e confirme.
2. Preencha os campos marcados com `*`.
3. Informe o número definido pela CSTIC. Esta versão não gera números sequenciais.
4. Mantenha ou altere a versão e o aprovador.
5. Se não houver requisitos, preencha **Não se aplica**.
6. No procedimento, informe o nome da primeira ação: por exemplo, **Abrir computador**.
7. Escreva a primeira subetapa: **Retirar os parafusos**.
8. Clique em **Adicionar subetapa** e escreva **Retirar a tampa**.
9. Clique em **Adicionar ação principal** e nomeie **Limpeza**.
10. Acrescente as subetapas dessa ação; a numeração reinicia em **1.1**.
11. Use `↑` e `↓` para mudar a ordem. As descrições e subetapas acompanham sua ação.
12. Use `×` para remover. A última ação e a última subetapa de uma ação não podem
    ser removidas, porque o documento precisa conter um procedimento.
13. Se quiser uma orientação complementar, use **Adicionar detalhe em marcador**.
14. Gere e baixe o documento.

### 2.8 Validar e atualizar uma base existente

1. Na tela inicial, escolha **Validar base existente**. Se estiver criando um POP,
   use **Escolher outra opção** ou a marca CSTIC para voltar à entrada.
2. Em **Arquivo da base de conhecimento**, selecione um **Word `.docx` de até 2 MB**.
   O arquivo precisa seguir o modelo CSTIC: cabeçalho de identificação e cinco
   seções em tabelas. Pode usar um Word gerado neste site ou preenchido manualmente
   no modelo original. PDF e `.doc` não são aceitos.
3. Clique em **Carregar para revisão**. O formulário aparece preenchido quando a
   leitura é concluída. A mensagem mostra o arquivo carregado e a data nele encontrada.
4. Leia os avisos. Um documento antigo sem ações `1º`, `2º`… terá suas instruções
   agrupadas em **Procedimento importado**; confira e reorganize conforme necessário.
5. Confira número, título, autor, aprovador e versão. Você decide se a revisão
   exige mudar o autor/versão; esses campos não são alterados automaticamente.
6. Edite objetivo, resultado, requisitos e executor. Corrija campos vazios/fora do
   padrão antes de gerar o novo arquivo.
7. Revise ações, subetapas e detalhes. Pode adicionar, remover e reordenar como
   na criação. Em cada ação, a numeração continua reiniciando em **1.1**.
8. Clique em **Gerar base revisada** e salve o download. O documento recebe a data
   atual, calculada no horário de Recife, e mantém o padrão do modelo CSTIC.
9. Para revisar outro arquivo, use **Limpar revisão** ou selecione/carregue outro
   Word. Se houver alterações feitas, o sistema pede confirmação para substituí-las.

A revisão significa conferência pelo técnico. Não há assinatura, aprovação digital,
login ou histórico; guarde o documento baixado no local usado pela seção.
O arquivo enviado é processado em memória, sem ser salvo pelo aplicativo.

**Se a importação falhar:** o aviso indica formato, estrutura ou tamanho inválido.
A revisão já preenchida permanece disponível. Não basta renomear PDF para `.docx`.
Para um `.doc` antigo, abra no Word/LibreOffice e salve como `.docx`, mantendo o modelo.
Documentos com imagens/objetos ou alterações controladas não são aceitos: faça uma
cópia somente com texto e finalize as alterações antes de importar. O site importa
conteúdo textual; fontes, cores e outros ajustes do Word recebido são substituídos
pelo formato padronizado do template. Textos acima dos limites são rejeitados,
sem cortar conteúdo.

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
   `IIS AppPool\CsticPop`. O aplicativo não precisa gravar POPs nessa pasta.
10. Mantenha **Autenticação Anônima** habilitada para o comportamento sem login.
11. Acesse o endereço configurado e gere um POP de teste.

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
docker run -d --name cstic-pop --restart unless-stopped -p 127.0.0.1:5080:8080 cstic-pop:1.0
```

Abra `http://localhost:5080`.

Para permitir rede local, use o mapeamento sem o endereço de loopback:

```bash
docker run -d --name cstic-pop --restart unless-stopped -p 5080:8080 cstic-pop:1.0
```

Use uma das duas opções; não crie dois containers com o mesmo nome. Se o container
já existir e quiser recriá-lo, primeiro pare/remova o anterior. Não há documentos
armazenados dentro dele, mas confira se é realmente o container deste aplicativo.

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

A porta 8080 do aplicativo permanece apenas na rede interna do Compose. Os
volumes `caddy_data` e `caddy_config` retêm os dados/certificados do proxy.
Não remova esses volumes desnecessariamente.

### 7.4 Atualizar o site

Copie o código atualizado e execute novamente:

```bash
docker compose up -d --build
```

Para parar os serviços sem remover os volumes:

```bash
docker compose down
```

Como você pediu **sem login**, qualquer pessoa que consiga acessar o endereço
pode preencher e gerar um POP. Para uso exclusivo por técnicos, a infraestrutura
pode restringir o acesso pela rede/VPN institucional ou pelo proxy existente.
O aplicativo não implementa aprovação eletrônica: informar um aprovador apenas
preenche seu nome no documento.

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

O runtime **ASP.NET Core 10** precisa estar instalado. Essa publicação depende
do runtime; não é um executável autocontido. Mantenha o diretório como diretório
de trabalho, para que os arquivos de CSS, JavaScript e imagens sejam encontrados.

## 9. Diagnóstico de problemas

| Problema | Como resolver |
|---|---|
| `dotnet` não é reconhecido | Instale o SDK .NET 10, feche/abra o terminal e execute `dotnet --info`. |
| SDK incompatível/global.json | Use SDK 10.0.x. A opção `latestFeature` permite versões de manutenção/features do .NET 10. |
| Projeto não encontrado | Entre na pasta que contém `src` e `README.md`; confira o caminho do comando. |
| Porta já em uso | Pare a instância anterior ou use `--urls http://localhost:5081`; abra a mesma porta no navegador. |
| Abre no servidor, mas não na rede | Use `0.0.0.0`, o IP real do servidor, a porta certa e confira firewall/rota/VPN. |
| Página sem estilo na publicação | Execute a DLL de dentro da pasta publicada e preserve a pasta `wwwroot`. |
| Não gera o Word | Preencha os campos obrigatórios e todas as ações/subetapas; veja a mensagem no formulário. |
| Mensagem de formulário expirado | O servidor pode ter reiniciado. Recarregue para obter um novo token; o formulário não tem autosave. |
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

## 10. Manutenção e mudanças futuras

- Layout do site: `wwwroot/css/site.css`.
- Tela inicial: `Pages/Index.cshtml`.
- Campos/estrutura do editor: `Pages/Editor.cshtml`.
- Ações/subetapas dinâmicas e download: `wwwroot/js/form.js`.
- Dados e regras: `Models/KnowledgeArticle.cs` e `Services/ArticleValidator.cs`.
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

A opção **Validar base existente** importa o texto do Word para o editor e gera um
novo arquivo revisado; o arquivo original no computador do técnico não é alterado.
Salvar rascunhos e guardar histórico podem ser adicionados depois, se a seção precisar.
