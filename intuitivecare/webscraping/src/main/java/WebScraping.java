import java.io.*;
import java.net.*;
import java.nio.file.*;
import java.util.*;
import java.util.zip.*;

public class WebScraping {
    public static void main(String[] args) {
        String anexoI = "https://www.gov.br/ans/pt-br/acesso-a-informacao/participacao-da-sociedade/atualizacao-do-rol-de-procedimentos/Anexo_I_Rol_2021RN_465.2021_RN627L.2024.pdf";
        String anexoII = "https://www.gov.br/ans/pt-br/acesso-a-informacao/participacao-da-sociedade/atualizacao-do-rol-de-procedimentos/Anexo_II_DUT_2021_RN_465.2021_RN628.2025_RN629.2025.pdf";
        String downloadDir = System.getProperty("user.home") + File.separator + "Downloads";
        String zipFileName = downloadDir + File.separator + "anexos.zip";

        Scanner scanner = new Scanner(System.in);
        System.out.println("Qual arquivo você quer fazer download:");
        System.out.println("1 - anexo I");
        System.out.println("2 - anexo II");
        System.out.println("3 - ambos");
        System.out.println("4 - Exit");
        System.out.print("Opção: ");
        String opcao = scanner.nextLine();

        List<String> urlsSelecionadas = new ArrayList<>();
        switch (opcao) {
            case "1":
                urlsSelecionadas.add(anexoI);
                break;
            case "2":
                urlsSelecionadas.add(anexoII);
                break;
            case "3":
                urlsSelecionadas.add(anexoI);
                urlsSelecionadas.add(anexoII);
                break;
            case "4":
                System.out.println("Download cancelado");
                scanner.close();
                return;
            default:
                System.out.println("Opção inválida. Encerrando.");
                scanner.close();
                return;
        }
        scanner.close();

        List<String> arquivosBaixados = new ArrayList<>();

        try {
            for (String url : urlsSelecionadas) {
                String arquivoBaixado = download(url, downloadDir);
                arquivosBaixados.add(arquivoBaixado);
            }

            zipFiles(arquivosBaixados, zipFileName);
            System.out.println("Download e compactação concluídos com sucesso.");
            System.out.println("O arquivo ZIP foi criado em: " + zipFileName);
        } catch (IOException e) {
            e.printStackTrace();
        }
    }

    private static String download(String fileURL, String saveDir) throws IOException {
        URL url = new URL(fileURL);
        HttpURLConnection connection = (HttpURLConnection) url.openConnection();
        connection.setRequestProperty("User-Agent", "Mozilla/5.0");

        if (connection.getResponseCode() != HttpURLConnection.HTTP_OK) {
            throw new IOException("HTTP " + connection.getResponseCode() + " - " + connection.getResponseMessage());
        }

        String fileName = Paths.get(url.getPath()).getFileName().toString();
        Path savePath = Paths.get(saveDir, fileName);

        try (InputStream in = connection.getInputStream()) {
            Files.copy(in, savePath, StandardCopyOption.REPLACE_EXISTING);
        }
        System.out.println("Baixado: " + fileName);
        return fileName;
    }

    private static void zipFiles(List<String> filesToZip, String zipFileName) throws IOException {
        if (filesToZip.isEmpty()) {
            return;
        }
        try (FileOutputStream fos = new FileOutputStream(zipFileName);
             ZipOutputStream zos = new ZipOutputStream(fos)) {
            for (String fileName : filesToZip) {
                File file = new File(System.getProperty("user.home") + File.separator + "Downloads", fileName);
                try (FileInputStream fis = new FileInputStream(file)) {
                    zos.putNextEntry(new ZipEntry(file.getName()));
                    byte[] buffer = new byte[1024];
                    int len;
                    while ((len = fis.read(buffer)) > 0) {
                        zos.write(buffer, 0, len);
                    }
                    zos.closeEntry();
                }
            }
        }
        for (String fileName : filesToZip) {
            new File(System.getProperty("user.home") + File.separator + "Downloads", fileName).delete();
        }
        System.out.println("Compactado: " + zipFileName);
    }
}