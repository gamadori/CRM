using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;

namespace CRM.Server.Services
{
    public interface IDocxToPdfConverter
    {
        byte[] ConvertToPdf(byte[] docxBytes);
    }

    public class LibreOfficeDocxToPdfConverter : IDocxToPdfConverter
    {
        private readonly string _libreOfficePath;
        private readonly string _tempDirectory;

        public LibreOfficeDocxToPdfConverter(IConfiguration configuration)
        {
            // Leggi configurazione da appsettings.json (se presente)
            var configPath = configuration["LibreOffice:ExecutablePath"];
            var configTempDir = configuration["LibreOffice:TempDirectory"];

            // Percorso LibreOffice: usa config se presente, altrimenti auto-detect OS
            if (!string.IsNullOrWhiteSpace(configPath))
            {
                _libreOfficePath = configPath;
            }
            else
            {
                // Auto-detect basato su OS
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _libreOfficePath = @"C:\Program Files\LibreOffice\program\soffice.exe";
                    // Alternative comuni Windows:
                    if (!File.Exists(_libreOfficePath))
                        _libreOfficePath = @"C:\Program Files (x86)\LibreOffice\program\soffice.exe";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    _libreOfficePath = "/usr/bin/soffice";
                    // Alternative: /usr/local/bin/soffice, /opt/libreoffice/program/soffice
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    _libreOfficePath = "/Applications/LibreOffice.app/Contents/MacOS/soffice";
                }
                else
                {
                    throw new PlatformNotSupportedException("OS non supportato per LibreOffice conversion");
                }
            }

            // Directory temporanea: usa config se presente, altrimenti system temp
            if (!string.IsNullOrWhiteSpace(configTempDir))
            {
                _tempDirectory = configTempDir;
            }
            else
            {
                _tempDirectory = Path.Combine(Path.GetTempPath(), "LibreOfficeConversion");
            }

            if (!Directory.Exists(_tempDirectory))
                Directory.CreateDirectory(_tempDirectory);
        }

        public byte[] ConvertToPdf(byte[] docxBytes)
        {
            if (docxBytes == null || docxBytes.Length == 0)
                throw new ArgumentException("DOCX bytes is null or empty");

            if (!File.Exists(_libreOfficePath))
                throw new FileNotFoundException($"LibreOffice non trovato in: {_libreOfficePath}. Installa LibreOffice sul server o configura 'LibreOffice:ExecutablePath' in appsettings.json");

            // Crea directory temporanea unica per questa conversione
            var conversionId = Guid.NewGuid().ToString();
            var workDir = Path.Combine(_tempDirectory, conversionId);
            Directory.CreateDirectory(workDir);

            var inputFile = Path.Combine(workDir, "input.docx");
            var outputPdfFile = Path.Combine(workDir, "input.pdf");

            try
            {
                // Salva DOCX temporaneo
                File.WriteAllBytes(inputFile, docxBytes);

                // Chiama LibreOffice headless per conversione
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = _libreOfficePath,
                    Arguments = $"--headless --convert-to pdf --outdir \"{workDir}\" \"{inputFile}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = workDir
                };

                using (var process = new Process { StartInfo = processStartInfo })
                {
                    process.Start();

                    // Timeout 30 secondi per conversione
                    if (!process.WaitForExit(30000))
                    {
                        process.Kill();
                        throw new TimeoutException("LibreOffice conversion timeout (>30s)");
                    }

                    if (process.ExitCode != 0)
                    {
                        var error = process.StandardError.ReadToEnd();
                        throw new InvalidOperationException($"LibreOffice conversion failed (exit code {process.ExitCode}): {error}");
                    }
                }

                // Leggi PDF generato
                if (!File.Exists(outputPdfFile))
                    throw new FileNotFoundException($"PDF non generato da LibreOffice in: {outputPdfFile}");

                var pdfBytes = File.ReadAllBytes(outputPdfFile);
                return pdfBytes;
            }
            finally
            {
                // Cleanup: elimina directory temporanea
                try
                {
                    if (Directory.Exists(workDir))
                        Directory.Delete(workDir, true);
                }
                catch (Exception ex)
                {
                    // Log cleanup error ma non bloccare (best effort)
                    Console.Error.WriteLine($"Errore cleanup temp dir {workDir}: {ex.Message}");
                }
            }
        }
    }
}
