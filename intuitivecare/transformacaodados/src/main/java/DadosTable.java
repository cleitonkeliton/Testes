import java.io.*;
import java.util.*;
import java.util.zip.*;
import org.apache.pdfbox.pdmodel.PDDocument;
import org.apache.pdfbox.text.PDFTextStripper;
import technology.tabula.*;
import technology.tabula.extractors.BasicExtractionAlgorithm;
import com.opencsv.CSVWriter;

public class DadosTable {
    private static final Map<String, String> LEGENDAS = new HashMap<>();

    public static void main(String[] args) {
        try {
            // Configurações
            String pdfPath = "/home/valdevino/Downloads/intuitivecare/intuitivecare/transformacaodados/Anexo_I.pdf";
            String csvPath = "procedimentos.csv";
            String zipPath = "Cleiton_Valdevino.zip";

            // Processamento
            List<String[]> dados = extrairDadosPDF(pdfPath);
            if (!dados.isEmpty()) {
                substituirCabecalhos(dados.get(0));
                salvarCSV(dados, csvPath);
                compactarCSV(csvPath, zipPath);
                System.out.println("Processo concluído com sucesso!");
            } else {
                System.out.println("Nenhuma tabela encontrada no PDF.");
            }

        } catch (Exception e) {
            System.err.println("Erro: " + e.getMessage());
            e.printStackTrace();
        }
    }

    private static List<String[]> extrairDadosPDF(String pdfPath) throws Exception {
        List<String[]> dados = new ArrayList<>();

        File file = new File(pdfPath);


        if (!file.exists()) {
            throw new FileNotFoundException("Arquivo não encontrado: " + file.getAbsolutePath());
        }

        try (PDDocument document = PDDocument.load(file)) {
            extrairLegendas(document);

            ObjectExtractor oe = new ObjectExtractor(document);
            BasicExtractionAlgorithm bea = new BasicExtractionAlgorithm();

            for (PageIterator it = oe.extract(); it.hasNext(); ) {
                Page page = it.next();
                for (Table table : bea.extract(page)) {
                    for (List<RectangularTextContainer> row : table.getRows()) {
                        List<String> linha = new ArrayList<>();
                        for (RectangularTextContainer cell : row) {
                            linha.add(cell.getText().replace(",", ";"));
                        }
                        dados.add(linha.toArray(new String[0]));
                    }
                }
            }
        }
        return dados;
    }

    private static void extrairLegendas(PDDocument document) throws Exception {
        PDFTextStripper stripper = new PDFTextStripper();
        String textoCompleto = stripper.getText(document);

        Arrays.stream(textoCompleto.split("\n"))
                .filter(linha -> linha.contains("OD:") || linha.contains("AMB:"))
                .forEach(linha -> {
                    String[] partes = linha.split(":");
                    if (partes.length >= 2) { // Verifica se há pelo menos um par chave-valor válido
                        LEGENDAS.put(partes[0].trim(), partes[1].trim());
                    }
                });
    }

    private static void substituirCabecalhos(String[] cabecalho) {
        for (int i = 0; i < cabecalho.length; i++) {
            if (LEGENDAS.containsKey(cabecalho[i])) {
                cabecalho[i] = LEGENDAS.get(cabecalho[i]);
            }
        }
    }

    private static void salvarCSV(List<String[]> dados, String caminhoCSV) throws Exception {
        try (CSVWriter writer = new CSVWriter(new FileWriter(caminhoCSV))) {
            writer.writeAll(dados);
        }
    }

    private static void compactarCSV(String csvPath, String zipPath) throws Exception {
        try (ZipOutputStream zos = new ZipOutputStream(new FileOutputStream(zipPath));
             FileInputStream fis = new FileInputStream(csvPath)) {

            zos.putNextEntry(new ZipEntry(new File(csvPath).getName()));
            byte[] buffer = new byte[1024];
            int length;

            while ((length = fis.read(buffer)) > 0) {
                zos.write(buffer, 0, length);
            }
            zos.closeEntry();
        }
        new File(csvPath).delete();
    }
}
