using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Cstic.KnowledgeBase.Data;
using Cstic.KnowledgeBase.Models;
using Cstic.KnowledgeBase.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var tests = new List<(string Name, Action Run)>();
KnowledgeArticle Example() => new()
{
    Code = "0001", Title = "Manutenção preventiva de computador administrativo", Version = "1.0",
    Author = "Técnico de exemplo", Approver = "Marcone Jose Roque Luna", Executor = "N2 / N1",
    Objective = "Padronizar a manutenção preventiva de um computador de uso administrativo.",
    Result = "Computador montado, com funcionamento verificado e eventuais defeitos registrados.",
    Requirements = "Ferramentas e materiais autorizados pela instituição; instruções do fabricante.\nComputador administrativo desligado e desconectado da alimentação.",
    Actions =
    [
        new() { Title = "Abrir computador", Steps = [
            new() { Instruction = "Retirar os parafusos.", Details = ["Identificar e guardar os parafusos para a montagem."] },
            new() { Instruction = "Retirar a tampa." }] },
        new() { Title = "Limpeza", Steps = [
            new() { Instruction = "Retirar os componentes necessários à limpeza." },
            new() { Instruction = "Realizar a limpeza conforme as instruções do fabricante.", Details = ["Utilizar materiais autorizados pela instituição."] }] },
        new() { Title = "Preventivas", Steps = [
            new() { Instruction = "Verificar a necessidade de manutenção da pasta térmica." },
            new() { Instruction = "Verificar o encaixe dos conectores." },
            new() { Instruction = "Identificar itens defeituosos." }] },
        new() { Title = "Montagem", Steps = [
            new() { Instruction = "Recolocar todas as peças." },
            new() { Instruction = "Fechar o gabinete." },
            new() { Instruction = "Recolocar os parafusos." },
            new() { Instruction = "Testar o funcionamento." }] }
    ]
};
void Assert(bool condition, string message = "Assertion failed")
{
    if (!condition) throw new InvalidOperationException(message);
}
XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
XDocument ReadXml(ZipArchive archive, string path)
{
    using var stream = archive.GetEntry(path)!.Open();
    return XDocument.Load(stream);
}
string Text(XDocument document) => string.Join(" ", document.Descendants(w + "t").Select(node => node.Value));
var documents = new DocumentService(new FixedClock());
tests.Add(("Documento válido", () => Assert(ArticleValidator.Validate(Example()).Count == 0)));
tests.Add(("Obrigatórios rejeitam vazio e espaços", () =>
{
    var article = Example(); article.Title = " "; article.Author = "";
    var errors = ArticleValidator.Validate(article);
    Assert(errors.ContainsKey("title") && errors.ContainsKey("author"));
}));
tests.Add(("Nível e versão fora do formato", () =>
{
    var article = Example(); article.Executor = "N3"; article.Version = "abc";
    var errors = ArticleValidator.Validate(article);
    Assert(errors.ContainsKey("executor") && errors.ContainsKey("version"));
}));
tests.Add(("Nenhuma etapa e lista nula", () =>
{
    var article = Example(); article.Actions = [];
    Assert(ArticleValidator.Validate(article).ContainsKey("actions"));
    article.Actions = null;
    Assert(ArticleValidator.Validate(article).ContainsKey("actions"));
}));
tests.Add(("Limites de tamanho têm mensagens em português", () =>
{
    var article = Example(); article.Author = new string('a', 121); article.Objective = new string('a', 5001);
    var errors = ArticleValidator.Validate(article);
    Assert(errors["author"].Single().Contains("120 caracteres") && errors["objective"].Single().Contains("5.000 caracteres"));
}));
tests.Add(("Campos de etapa identificados pelo índice", () =>
{
    var article = Example(); article.Actions![0].Steps![1].Instruction = "";
    Assert(ArticleValidator.Validate(article).ContainsKey("actions[0].steps[1].instruction"));
}));
tests.Add(("Itens nulos não causam exceção", () =>
{
    var article = Example(); article.Actions![0].Steps![0] = null!;
    Assert(ArticleValidator.Validate(article).ContainsKey("actions[0].steps"));
    article = Example(); article.Actions![0].Steps![0].Details = [null!];
    Assert(ArticleValidator.Validate(article).ContainsKey("actions[0].steps[0].details[0]"));
}));
tests.Add(("Limite de 100 etapas", () =>
{
    var article = Example(); article.Actions![0].Steps = Enumerable.Range(0, 101).Select(i => new ProcedureStep { Instruction = "Ação" }).ToList();
    Assert(ArticleValidator.Validate(article).ContainsKey("actions[0].steps"));
}));
tests.Add(("Limites de texto e detalhes", () =>
{
    var article = Example(); article.Actions![0].Steps![0].Instruction = new string('a', 501);
    article.Actions![0].Steps![1].Details = Enumerable.Repeat("Detalhe", 21).ToList();
    var errors = ArticleValidator.Validate(article);
    Assert(errors.ContainsKey("actions[0].steps[0].instruction") && errors.ContainsKey("actions[0].steps[1].details"));
}));
tests.Add(("Caracteres de controle XML são rejeitados", () =>
{
    var article = Example(); article.Objective = "Texto\u0001inválido";
    Assert(ArticleValidator.Validate(article).ContainsKey("objective"));
}));
tests.Add(("Detalhes opcionais podem ficar vazios", () =>
{
    var article = Example(); article.Actions![0].Steps![0].Details = ["", " "];
    Assert(ArticleValidator.Validate(article).Count == 0);
    Assert(documents.CreateWord(article).Length > 1000);
}));
tests.Add(("Modelo mantém cinco tabelas, cabeçalho e campos", () =>
{
    using var memory = new MemoryStream(documents.CreateWord(Example()));
    using var zip = new ZipArchive(memory);
    foreach (var entry in zip.Entries.Where(entry => entry.FullName.EndsWith(".xml")))
        using (var stream = entry.Open()) XDocument.Load(stream);
    var body = ReadXml(zip, "word/document.xml");
    Assert(body.Root!.Element(w + "body")!.Elements(w + "tbl").Count() == 5);
    Assert(Text(body).Contains("OBJETIVO") && Text(body).Contains("RESULTADO") && Text(body).Contains("REQUISITOS"));
    var header = Text(ReadXml(zip, "word/header3.xml"));
    Assert(header.Contains("PROCEDIMENTO OPERACIONAL PADRÃO - POP") && header.Contains("CSTIC"));
    Assert(header.Contains("0001") && header.Contains("Técnico de exemplo") && header.Contains("Marcone Jose Roque Luna"));
    Assert(!header.Contains("Assunto da base") && !header.Contains("Geração automatica"));
}));
tests.Add(("Data automática respeita Recife ao virar o dia UTC", () =>
{
    using var memory = new MemoryStream(documents.CreateWord(Example()));
    using var zip = new ZipArchive(memory);
    Assert(Text(ReadXml(zip, "word/header3.xml")).Contains("07/10/2026"));
}));
tests.Add(("Ações 1º a 4º e subetapas reiniciam em 1.1", () =>
{
    var article = Example();
    using var memory = new MemoryStream(documents.CreateWord(article));
    using var zip = new ZipArchive(memory);
    var document = ReadXml(zip, "word/document.xml");
    var paragraphs = document.Descendants(w + "p").Select(p => string.Concat(p.Descendants(w + "t").Select(t => t.Value))).ToArray();
    for (var i = 1; i <= 4; i++) Assert(paragraphs.Any(p => p.StartsWith($"{i}º  ")));
    Assert(paragraphs.Count(p => p.StartsWith("1.1  ")) == 4);
    Assert(paragraphs.Count(p => p.StartsWith("1.2  ")) == 4);
    Assert(paragraphs.Count(p => p.StartsWith("1.3  ")) == 2);
    Assert(paragraphs.Count(p => p.StartsWith("1.4  ")) == 1);
    var footer = ReadXml(zip, "word/footer3.xml");
    Assert(Text(footer).Contains(article.Title));
    Assert(footer.Descendants(w + "instrText").Any(node => node.Value.Contains("PAGE")));
}));
tests.Add(("Símbolos, acentos e quebras de linha permanecem texto", () =>
{
    var article = Example(); article.Objective = "Ação <script> & teste\nOutra linha";
    using var memory = new MemoryStream(documents.CreateWord(article));
    using var zip = new ZipArchive(memory);
    var xml = ReadXml(zip, "word/document.xml");
    Assert(Text(xml).Contains("Ação <script> & teste"));
    Assert(xml.Descendants(w + "br").Any());
    Assert(!xml.Descendants().Any(node => node.Name.LocalName == "script"));
}));
tests.Add(("Arquivo sem caminhos ou caracteres impróprios", () =>
{
    var article = Example(); article.Code = "CSTIC/0001"; article.Title = "Ação de impressão";
    var filename = DocumentService.FileName(article);
    Assert(filename == "POP_CSTIC_0001_Acao_de_impressao.docx", filename);
}));
tests.Add(("Documentos simultâneos não compartilham preenchimento", () =>
{
    Parallel.For(0, 12, index =>
    {
        var article = Example(); article.Title = $"Documento {index:D2}";
        using var memory = new MemoryStream(documents.CreateWord(article));
        using var zip = new ZipArchive(memory);
        Assert(Text(ReadXml(zip, "word/header3.xml")).Contains(article.Title));
    });
}));

tests.Add(("Ação vazia, ação nula e subetapas vazias são rejeitadas", () =>
{
    var article = Example(); article.Actions![0].Title = " ";
    article.Actions[1].Steps = [];
    article.Actions[2] = null!;
    var errors = ArticleValidator.Validate(article);
    Assert(errors.ContainsKey("actions[0].title") && errors.ContainsKey("actions[1].steps") && errors.ContainsKey("actions"));
}));
tests.Add(("Limite de 20 ações e 100 subetapas somadas", () =>
{
    var article = Example();
    article.Actions = Enumerable.Range(0, 21).Select(i => new ProcedureAction { Title = "Ação", Steps = [new() { Instruction = "Subetapa" }] }).ToList();
    Assert(ArticleValidator.Validate(article).ContainsKey("actions"));
    article.Actions = Enumerable.Range(0, 2).Select(i => new ProcedureAction { Title = "Ação", Steps = Enumerable.Range(0, 51).Select(j => new ProcedureStep { Instruction = "Subetapa" }).ToList() }).ToList();
    Assert(ArticleValidator.Validate(article).ContainsKey("actions"));
}));


var importer = new DocumentImportService();
byte[] ChangePart(byte[] original, string path, Func<XDocument, XDocument> change)
{
    using var buffer = new MemoryStream(); buffer.Write(original); buffer.Position = 0;
    using (var zip = new ZipArchive(buffer, ZipArchiveMode.Update, leaveOpen: true))
    {
        var xml = ReadXml(zip, path); zip.GetEntry(path)!.Delete();
        using var stream = zip.CreateEntry(path).Open(); change(xml).Save(stream);
    }
    return buffer.ToArray();
}
void Rejected(byte[] bytes)
{
    try { importer.Import(bytes); }
    catch (DocumentImportException) { return; }
    throw new InvalidOperationException("Arquivo deveria ser rejeitado");
}
tests.Add(("Importação preserva todos os campos, ações, subetapas e detalhes", () =>
{
    var source = Example(); var imported = importer.Import(documents.CreateWord(source));
    Assert(System.Text.Json.JsonSerializer.Serialize(source) == System.Text.Json.JsonSerializer.Serialize(imported.Article));
    Assert(imported.OriginalDate == "07/10/2026" && imported.Warnings.Count == 0);
}));
tests.Add(("Revisão, exportação e reimportação preservam edição", () =>
{
    var article = importer.Import(documents.CreateWord(Example())).Article;
    article.Title = "Base revisada"; article.Version = "1.1";
    article.Actions![1].Steps!.Add(new() { Instruction = "Nova instrução", Details = ["Detalhe novo"] });
    var reviewed = importer.Import(documents.CreateWord(article)).Article;
    Assert(reviewed.Title == "Base revisada" && reviewed.Version == "1.1");
    Assert(reviewed.Actions![1].Steps![2].Instruction == "Nova instrução");
    Assert(reviewed.Actions[1].Steps![2].Details![0] == "Detalhe novo");
}));
tests.Add(("Importação conserva quebras de linha e texto com símbolos", () =>
{
    var article = Example(); article.Objective = "Ação <script> & teste\nLinha 2";
    article.Actions![0].Title = "Abrir\ncomputador";
    article.Actions[0].Steps![0].Instruction = "Retirar\nos parafusos";
    article.Actions[0].Steps![0].Details = ["Linha 1\nLinha 2"];
    var result = importer.Import(documents.CreateWord(article)).Article;
    Assert(result.Objective == article.Objective);
    Assert(result.Actions![0].Title == article.Actions[0].Title);
    Assert(result.Actions[0].Steps![0].Instruction == article.Actions[0].Steps![0].Instruction);
    Assert(result.Actions[0].Steps![0].Details![0] == "Linha 1\nLinha 2");
}));
tests.Add(("Modelo original vazio pode ser carregado para completar", () =>
{
    using var resource = typeof(DocumentService).Assembly.GetManifestResourceStream("Cstic.KnowledgeBase.Templates.POP_BasedeConhecimento_CSTIC.docx")!;
    using var memory = new MemoryStream(); resource.CopyTo(memory);
    var result = importer.Import(memory.ToArray());
    Assert(result.Article.Title == "" && result.Article.Version == "1.0" && result.Warnings.Count > 0);
}));
tests.Add(("Numeração automática antiga é preservada e solicita conferência", () =>
{
    var bytes = ChangePart(documents.CreateWord(Example()), "word/document.xml", xml =>
    {
        var cell = xml.Root!.Element(w + "body")!.Elements(w + "tbl").Last().Elements(w + "tr").Last().Element(w + "tc")!;
        cell.Elements(w + "p").Remove();
        cell.Add(new XElement(w + "p", new XElement(w + "pPr", new XElement(w + "numPr", new XElement(w + "ilvl", new XAttribute(w + "val", "0")), new XElement(w + "numId", new XAttribute(w + "val", "3")))), new XElement(w + "r", new XElement(w + "t", "Primeiro passo"))));
        cell.Add(new XElement(w + "p", new XElement(w + "pPr", new XElement(w + "numPr", new XElement(w + "ilvl", new XAttribute(w + "val", "1")), new XElement(w + "numId", new XAttribute(w + "val", "3")))), new XElement(w + "r", new XElement(w + "t", "Cuidado adicional"))));
        return xml;
    });
    var result = importer.Import(bytes);
    Assert(result.Article.Actions![0].Steps![0].Instruction == "Primeiro passo");
    Assert(result.Article.Actions[0].Steps![0].Details![0] == "Cuidado adicional" && result.Warnings.Count > 0);
}));
tests.Add(("Arquivo inválido, vazio e maior que 2 MB rejeitados", () =>
{
    Rejected(Encoding.UTF8.GetBytes("%PDF-1.7")); Rejected([]); Rejected(new byte[DocumentImportService.MaxFileBytes + 1]);
}));
tests.Add(("Word fora do modelo CSTIC é rejeitado", () =>
{
    Rejected(ChangePart(documents.CreateWord(Example()), "word/document.xml", xml => { xml.Descendants(w + "tbl").First().Remove(); return xml; }));
}));
tests.Add(("Imagens e alterações controladas não são descartadas silenciosamente", () =>
{
    foreach (var name in new[] { "drawing", "ins" })
        Rejected(ChangePart(documents.CreateWord(Example()), "word/document.xml", xml => { xml.Descendants(w + "tc").First().Add(new XElement(w + name)); return xml; }));
}));
tests.Add(("Textos acima dos limites são rejeitados sem cortar conteúdo", () =>
{
    Rejected(ChangePart(documents.CreateWord(Example()), "word/document.xml", xml =>
    {
        xml.Root!.Element(w + "body")!.Elements(w + "tbl").First().Elements(w + "tr").Last().Descendants(w + "t").First().Value = new string('a', 5001);
        return xml;
    }));
}));
tests.Add(("Importação rejeita POP maior que o limite de geração", () =>
{
    var article = Example();
    article.Actions = Enumerable.Range(0, 20).Select(i => new ProcedureAction
    {
        Title = $"Ação {i}",
        Steps = Enumerable.Range(0, 5).Select(j => new ProcedureStep
            { Instruction = $"Subetapa {j}", Details = [new string('á', 900), new string('b', 900)] }).ToList()
    }).ToList();
    Assert(ArticleValidator.Validate(article).Count == 0, "Cada campo deve respeitar seu limite individual");
    Assert(ArticleValidator.PayloadLength(article) > ArticleValidator.MaxPayloadLength);
    try { importer.Import(documents.CreateWord(article)); }
    catch (DocumentImportException exception) { Assert(exception.Message.Contains("180.000")); return; }
    throw new InvalidOperationException("POP acima do limite deveria ser rejeitado na importação");
}));
tests.Add(("ZIP com entrada duplicada é rejeitado", () =>
{
    using var memory = new MemoryStream(); memory.Write(documents.CreateWord(Example())); memory.Position = 0;
    using (var zip = new ZipArchive(memory, ZipArchiveMode.Update, leaveOpen: true))
        using (var stream = zip.CreateEntry("word/document.xml").Open()) stream.Write(Encoding.UTF8.GetBytes("<x/>"));
    Rejected(memory.ToArray());
}));
tests.Add(("XML com DTD é rejeitado sem resolver conteúdo externo", () =>
{
    using var memory = new MemoryStream(); memory.Write(documents.CreateWord(Example())); memory.Position = 0;
    using (var zip = new ZipArchive(memory, ZipArchiveMode.Update, leaveOpen: true))
    {
        zip.GetEntry("word/document.xml")!.Delete();
        using var stream = zip.CreateEntry("word/document.xml").Open();
        stream.Write(Encoding.UTF8.GetBytes("<!DOCTYPE x [<!ENTITY external SYSTEM 'file:///etc/passwd'>]><x>&external;</x>"));
    }
    Rejected(memory.ToArray());
}));
tests.Add(("Documento expandido acima do limite é rejeitado", () =>
{
    using var memory = new MemoryStream(); memory.Write(documents.CreateWord(Example())); memory.Position = 0;
    using (var zip = new ZipArchive(memory, ZipArchiveMode.Update, leaveOpen: true))
        using (var stream = zip.CreateEntry("extra.bin", CompressionLevel.Optimal).Open()) stream.Write(new byte[13 * 1024 * 1024]);
    Rejected(memory.ToArray());
}));

// Accounts and POP workflow, each test with an empty in-memory database.
(AccountService Accounts, PopService Pops, AppDbContext Db) NewDatabase()
{
    var connection = new SqliteConnection("DataSource=:memory:");
    connection.Open();
    var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
    db.Database.Migrate();
    return (new AccountService(db, new FixedClock(), new PasswordHasher<UserAccount>()), new PopService(db, new FixedClock(), documents), db);
}
void Fails<T>(Func<Task> action, string expected) where T : Exception
{
    try { action().GetAwaiter().GetResult(); }
    catch (T exception) { Assert(exception.Message.Contains(expected), $"Mensagem inesperada: {exception.Message}"); return; }
    throw new InvalidOperationException($"Deveria falhar com '{expected}'");
}
(AccountService Accounts, PopService Pops, AppDbContext Db, UserAccount Admin, UserAccount Tech) Team()
{
    var (accounts, pops, db) = NewDatabase();
    accounts.EnsureInitialAdminAsync("adm.audi", "Administrador CSTIC", "Qualidade", "Senha@123456").Wait();
    var admin = db.Users.Single();
    var tech = accounts.RegisterAsync("Maria José da Silva", "Técnico N1", "maria.silva", "Manutencao2026", "Manutencao2026").Result;
    accounts.ApproveAsync(admin, tech.Id, UserRole.Tecnico).Wait();
    return (accounts, pops, db, admin, tech);
}
tests.Add(("Administrador inicial exige troca de senha e não é recriado", () =>
{
    var (accounts, _, db) = NewDatabase();
    accounts.EnsureInitialAdminAsync("adm.audi", "Administrador CSTIC", "Qualidade", "Senha@123456").Wait();
    accounts.EnsureInitialAdminAsync("adm.audi", "Administrador CSTIC", "Qualidade", "Outra@123456").Wait();
    var admin = db.Users.Single();
    Assert(admin.IsAdmin && admin.MustChangePassword && admin.Status == UserStatus.Ativo);
    Assert(!admin.PasswordHash.Contains("Senha@123456"), "Senha não pode ser guardada em texto");
    var result = accounts.SignInAsync("adm.audi", "Senha@123456").Result;
    Assert(result.User?.Id == admin.Id);
}));
tests.Add(("Cadastro normaliza login e só entra após aprovação", () =>
{
    var (accounts, _, db) = NewDatabase();
    accounts.EnsureInitialAdminAsync("adm.audi", "Administrador CSTIC", "Qualidade", "Senha@123456").Wait();
    var user = accounts.RegisterAsync("  José   Souza ", "Técnico N2", " José.Souza ", "Manutencao2026", "Manutencao2026").Result;
    Assert(user.Login == "jose.souza" && user.FullName == "José Souza" && user.Status == UserStatus.Pendente);
    Assert(accounts.SignInAsync("jose.souza", "Manutencao2026").Result.Error!.Contains("aguarda"));
    Assert(accounts.SignInAsync("jose.souza", "errada12345").Result.Error == "Login ou senha incorretos.", "Situação só aparece com a senha correta");
    accounts.ApproveAsync(db.Users.First(item => item.Role == UserRole.Administrador), user.Id, UserRole.Tecnico).Wait();
    Assert(accounts.SignInAsync("JOSE.SOUZA", "Manutencao2026").Result.User?.Id == user.Id);
}));
tests.Add(("Cadastro valida login, nome, função e senha", () =>
{
    var (accounts, _, _) = NewDatabase();
    foreach (var login in new[] { "maria", "maria silva", "maria.silva.souza", "maria.silva123", "m@ria.silva" })
        Fails<AccountException>(() => accounts.RegisterAsync("Maria Silva", "Técnico N1", login, "Manutencao2026", "Manutencao2026"), "primeironome.ultimonome");
    Fails<AccountException>(() => accounts.RegisterAsync("Maria", "Técnico N1", "maria.silva", "Manutencao2026", "Manutencao2026"), "nome completo");
    Fails<AccountException>(() => accounts.RegisterAsync("Maria Silva", "Gerente", "maria.silva", "Manutencao2026", "Manutencao2026"), "função");
    Fails<AccountException>(() => accounts.RegisterAsync("Maria Silva", "Técnico N1", "maria.silva", "curta1", "curta1"), "10 a 128");
    Fails<AccountException>(() => accounts.RegisterAsync("Maria Silva", "Técnico N1", "maria.silva", "somenteletras", "somenteletras"), "letra e um número");
    Fails<AccountException>(() => accounts.RegisterAsync("Maria Silva", "Técnico N1", "maria.silva", "maria.silva2026", "maria.silva2026"), "login");
    Fails<AccountException>(() => accounts.RegisterAsync("Maria Silva", "Técnico N1", "maria.silva", "Manutencao2026", "Manutencao2027"), "confirmação");
    accounts.RegisterAsync("Maria Silva", "Técnico N1", "maria.silva", "Manutencao2026", "Manutencao2026").Wait();
    Fails<AccountException>(() => accounts.RegisterAsync("Maria Souza", "Técnico N1", "Maria.Silva", "Manutencao2026", "Manutencao2026"), "maria.silva2");
    accounts.RegisterAsync("Maria Souza", "Técnico N1", "maria.silva2", "Manutencao2026", "Manutencao2026").Wait();
}));
tests.Add(("Cinco senhas incorretas bloqueiam o acesso", () =>
{
    var (accounts, _, _, _, _) = Team();
    for (var i = 0; i < AccountService.MaxFailedLogins; i++) accounts.SignInAsync("maria.silva", "errada12345").Wait();
    Assert(accounts.SignInAsync("maria.silva", "Manutencao2026").Result.Error!.Contains("Muitas tentativas"));
}));
tests.Add(("Redefinição pelo administrador troca a senha e encerra sessões", () =>
{
    var (accounts, _, _, _, tech) = Team();
    var stamp = tech.SecurityStamp;
    accounts.ResetPasswordAsync(tech.Id, "Provisoria2026", "Provisoria2026").Wait();
    Assert(tech.MustChangePassword && tech.SecurityStamp != stamp);
    Assert(accounts.SignInAsync("maria.silva", "Manutencao2026").Result.User is null);
    Assert(accounts.SignInAsync("maria.silva", "Provisoria2026").Result.User is not null);
    Fails<AccountException>(() => accounts.ChangePasswordAsync(tech, "errada", "Definitiva2026", "Definitiva2026"), "atual");
    accounts.ChangePasswordAsync(tech, "Provisoria2026", "Definitiva2026", "Definitiva2026").Wait();
    Assert(!tech.MustChangePassword);
}));
tests.Add(("Sempre resta um administrador ativo", () =>
{
    var (accounts, _, _, admin, tech) = Team();
    Fails<AccountException>(() => accounts.SetActiveAsync(admin, admin.Id, false), "próprio");
    Fails<AccountException>(() => accounts.UpdateAsync(admin, admin.Id, "Administrador CSTIC", "Qualidade", UserRole.Tecnico), "próprio");
    accounts.UpdateAsync(admin, tech.Id, "Maria José da Silva", "Supervisor", UserRole.Administrador).Wait();
    accounts.SetActiveAsync(tech, admin.Id, false).Wait();
    Fails<AccountException>(() => accounts.SetActiveAsync(admin, tech.Id, false), "pelo menos um administrador");
    accounts.SetActiveAsync(tech, admin.Id, true).Wait();
    Assert(accounts.SignInAsync("adm.audi", "Senha@123456").Result.User is not null);
}));
tests.Add(("Cadastro recusado é removido e desativado não entra", () =>
{
    var (accounts, _, db, admin, tech) = Team();
    var pending = accounts.RegisterAsync("Carlos Lima", "Outro", "carlos.lima", "Manutencao2026", "Manutencao2026").Result;
    accounts.RejectAsync(pending.Id).Wait();
    Assert(!db.Users.Any(item => item.Login == "carlos.lima"));
    accounts.SetActiveAsync(admin, tech.Id, false).Wait();
    Assert(accounts.SignInAsync("maria.silva", "Manutencao2026").Result.Error!.Contains("desativado"));
}));
tests.Add(("Recuperação pelo servidor gera senha temporária", () =>
{
    var (accounts, _, db) = NewDatabase();
    accounts.EnsureInitialAdminAsync("adm.audi", "Administrador CSTIC", "Qualidade", "Senha@123456").Wait();
    var password = accounts.EmergencyResetAsync("adm.audi").Result;
    Assert(accounts.SignInAsync("adm.audi", password).Result.User?.MustChangePassword == true);
    Assert(accounts.SignInAsync("adm.audi", "Senha@123456").Result.User is null);
}));
tests.Add(("Fluxo do POP: rascunho, envio, devolução e validação", () =>
{
    var (_, pops, _, admin, tech) = Team();
    var incomplete = Example(); incomplete.Objective = "";
    var (pop, errors) = pops.SubmitAsync(tech, null, incomplete).Result;
    Assert(errors.ContainsKey("objective") && pop.Status == PopStatus.Rascunho, "Incompleto fica em rascunho");
    (pop, errors) = pops.SubmitAsync(tech, pop.Id, Example()).Result;
    Assert(errors.Count == 0 && pop.Status == PopStatus.Enviado);
    Fails<PopException>(() => pops.SaveAsync(tech, pop.Id, Example()), "aguardando validação");
    Fails<PopException>(() => pops.ReturnAsync(admin, pop.Id, "ok"), "5 a 2.000");
    pops.ReturnAsync(admin, pop.Id, "Detalhar a limpeza.").Wait();
    Assert(pop.Status == PopStatus.Devolvido && pop.ReturnComment == "Detalhar a limpeza.");
    Fails<PopException>(() => pops.ValidateAsync(admin, pop.Id), "não está aguardando");
    pops.SubmitAsync(tech, pop.Id, Example()).Wait();
    var version = pops.ValidateAsync(admin, pop.Id).Result;
    Assert(version.Code == "0001" && version.Version == "1.0" && pop.Status == PopStatus.Validado && pop.ReturnComment is null);
    var header = PopService.Content(version.ContentJson);
    Assert(header.Author == "Maria José da Silva" && header.Approver == "Administrador CSTIC");
    Assert(pop.Events.Select(item => item.Type).SequenceEqual([PopEventType.Criado, PopEventType.Enviado, PopEventType.Devolvido, PopEventType.Enviado, PopEventType.Validado]));
    var second = pops.SubmitAsync(tech, null, Example()).Result.Pop;
    Assert(pops.ValidateAsync(admin, second.Id).Result.Code == "0002", "Numeração sequencial");
}));
tests.Add(("Revisão mantém o número, sobe a versão e preserva a anterior", () =>
{
    var (_, pops, _, admin, tech) = Team();
    var pop = pops.SubmitAsync(tech, null, Example()).Result.Pop;
    pops.ValidateAsync(admin, pop.Id).Wait();
    var revised = Example(); revised.Title = "Manutenção revisada";
    pops.SaveAsync(tech, pop.Id, revised).Wait();
    Assert(pop.Status == PopStatus.Rascunho && pop.StatusLabel == "Revisão em rascunho");
    using (var zip = new ZipArchive(new MemoryStream(pops.Download(pop, "1.0").Bytes)))
        Assert(Text(ReadXml(zip, "word/header3.xml")).Contains("Manutenção preventiva"), "Versão 1.0 continua igual");
    pops.SubmitAsync(tech, pop.Id, revised).Wait();
    var version = pops.ValidateAsync(admin, pop.Id).Result;
    Assert(version.Code == "0001" && version.Version == "1.1" && pop.Versions.Count == 2);
    using var latest = new ZipArchive(new MemoryStream(pops.Download(pop).Bytes));
    var text = Text(ReadXml(latest, "word/header3.xml"));
    Assert(text.Contains("Manutenção revisada") && text.Contains("1.1") && text.Contains("07/10/2026"), "Cabeçalho da revisão validada: " + text);
}));
tests.Add(("Rascunho baixado traz número e aprovador pendentes", () =>
{
    var (_, pops, _, _, tech) = Team();
    var pop = pops.SaveAsync(tech, null, Example()).Result;
    using var zip = new ZipArchive(new MemoryStream(pops.Download(pop).Bytes));
    var header = Text(ReadXml(zip, "word/header3.xml"));
    Assert(header.Contains(PopService.PendingCode) && header.Contains(PopService.PendingApprover) && header.Contains("Maria José da Silva"));
    var empty = pops.SaveAsync(tech, null, new KnowledgeArticle()).Result;
    Fails<PopException>(() => Task.FromResult(pops.Download(empty)), "obrigatórios");
}));
tests.Add(("Importação mantém número livre e versão do arquivo", () =>
{
    var (_, pops, _, admin, tech) = Team();
    var source = Example(); source.Code = "0042"; source.Version = "2.3";
    var imported = pops.ImportAsync(tech, importer.Import(documents.CreateWord(source)), "antigo.docx").Result;
    Assert(imported.Code == "0042" && imported.Version == "2.3" && imported.Status == PopStatus.Rascunho);
    var repeated = pops.ImportAsync(tech, importer.Import(documents.CreateWord(source)), "copia.docx").Result;
    Assert(repeated.Code is null, "Número já usado não é repetido");
    pops.SubmitAsync(tech, imported.Id, PopService.Content(imported.ContentJson)).Wait();
    var version = pops.ValidateAsync(admin, imported.Id).Result;
    Assert(version.Code == "0042" && version.Version == "2.4");
    pops.SubmitAsync(tech, repeated.Id, PopService.Content(repeated.ContentJson)).Wait();
    Assert(pops.ValidateAsync(admin, repeated.Id).Result.Code == "0043", "Próximo número após o maior existente");
}));
tests.Add(("Técnico só acessa os próprios POPs", () =>
{
    var (accounts, pops, _, admin, tech) = Team();
    var other = accounts.CreateByAdminAsync(admin, "João Pedro Lima", "Técnico N2", "joao.lima", UserRole.Tecnico, "Temporaria2026", "Temporaria2026").Result;
    var pop = pops.SaveAsync(tech, null, Example()).Result;
    Assert(!PopService.CanView(other, pop) && PopService.CanView(admin, pop) && PopService.CanView(tech, pop));
    Fails<PopException>(() => pops.SaveAsync(other, pop.Id, Example()), "não pode alterar");
    Assert(!PopService.CanEdit(admin, pop), "Administrador devolve em vez de editar");
}));
tests.Add(("Versão sobe a partir da última validada", () =>
{
    Assert(PopService.NextVersion(null) == "1.0" && PopService.NextVersion("1.0") == "1.1" && PopService.NextVersion("1.9") == "1.10");
    Assert(PopService.NextVersion("2") == "2.1" && PopService.NextVersion("1.0.3") == "1.1");
}));

var failures = 0;
foreach (var test in tests)
{
    try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception exception) { failures++; Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}"); }
}
Console.WriteLine($"{tests.Count - failures}/{tests.Count} verificações passaram.");
if (args.Length == 2 && args[0] == "--example")
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
    File.WriteAllBytes(args[1], new DocumentService(TimeProvider.System).CreateWord(Example()));
    Console.WriteLine($"Exemplo salvo: {args[1]}");
}
return failures == 0 ? 0 : 1;

internal sealed class FixedClock : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(2026, 10, 8, 1, 30, 0, TimeSpan.Zero);
}
