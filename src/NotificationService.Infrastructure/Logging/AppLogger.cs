using NotificationService.Domain.Settings;
using NotificationService.Domain.Interfaces;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.RegularExpressions;

namespace NotificationService.Infrastructure.Logging;

public class AppLogger : IAppLogger
{
    private readonly LogConfig _config;
    private readonly LogSettings _logSettings;
    private static readonly object _fileLock = new object();
    private bool _processamentoEmAndamento = true;

    public AppLogger(IOptions<LogConfig> config, IOptions<LogSettings> logSettings)
    {
        _config = config.Value ?? throw new ArgumentNullException(nameof(config));
        _logSettings = logSettings.Value;

        Directory.CreateDirectory(_config.PathLog);
    }

    public void FinalizarProcessamento()
    {
        lock (_fileLock)
        {
            _processamentoEmAndamento = false;
            VerificarRotacaoTodosArquivos();
        }
    }

    public void Info(string mensagem, int? idx = null)
    {
        string filePath = GetLogFilePath(idx);
        WriteLog(mensagem, idx, filePath);
    }

    public void InfoArquivo(string mensagem, int? idx, string nomeArquivo = null)
    {
        string fileName = !string.IsNullOrEmpty(nomeArquivo)
            ? nomeArquivo
            : $"{_config.FileName} {DateTime.Now:dd MM yyyy}";

        string filePath = Path.Combine(_config.PathLog, "Arquivos", $"{fileName}.txt");

        WriteLog(mensagem, idx, filePath);
    }

    public void Infoteste(string mensagem, int? idx = null)
    {
        string logFileName = idx != null
            ? $"[{idx}] {_config.FileName}"
            : _config.FileName;

        string currentDate = DateTime.Now.ToString("dd MM yyyy");
        string logPath = Path.Combine(_config.PathLog, $"{logFileName} {currentDate}.txt");

        VerificarRotacaoTodosArquivos();

        WriteLog(mensagem, idx, logPath);
    }

    public void ErroArquivo(string nomeArquivo, string mensagem, string eps, int? idx)
    {
        DateTime agora = DateTime.Now;

        string erroPath = Path.Combine(
            _config.PathLog,
            "Erro",
            eps,
            agora.Year.ToString(),
            agora.Month.ToString("D2"),
            agora.Day.ToString("D2")
        );

        Directory.CreateDirectory(erroPath);

        string logFilePath = Path.Combine(
            erroPath,
            $"{Path.GetFileNameWithoutExtension(nomeArquivo)}.log"
        );

        WriteLog(mensagem, idx, logFilePath);
    }

    // ---------- MÉTODO PRINCIPAL DE LOG (thread-safe) ----------
    private void WriteLog(string mensagem, int? idx, string caminhoArquivo)
    {
        lock (_fileLock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(caminhoArquivo)!);

            if (_processamentoEmAndamento)
                VerificarRotacaoArquivo(caminhoArquivo);

            string prefixoWorker = idx.HasValue ? $"Worker {idx}" : "Principal";

            using (var sw = new StreamWriter(caminhoArquivo, append: true, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
            {
                sw.AutoFlush = true; // Força escrita imediata para evitar buffering entre threads
                sw.WriteLine(
                    $"[{DateTime.Now:dd MM yyyy HH:mm:ss.fff}] [{prefixoWorker}] {mensagem}"
                );
            }

            ExibirNoConsole($"{prefixoWorker}: {mensagem}");
        }
    }

    // ---------- ROTAÇÃO APÓS OS FLUXOS ----------
    private void VerificarRotacaoTodosArquivos()
    {
        var arquivos = Directory.GetFiles(_config.PathLog, "*.txt");

        foreach (var arquivo in arquivos)
            VerificarRotacaoArquivo(arquivo);
    }

    private void VerificarRotacaoArquivo(string caminhoAtual)
    {
        FileInfo fi = new FileInfo(caminhoAtual);

        if (Regex.IsMatch(fi.Name, @"_\d+\.\w+$"))
            return;

        if (!fi.Exists || fi.Length < _logSettings.MaxFileSizeBytes)
            return;

        string caminho = fi.DirectoryName!;
        string nomeBase = Regex.Replace(
            Path.GetFileNameWithoutExtension(fi.Name),
            @"_\d+$",
            ""
        );
        string extensao = fi.Extension;

        int contador = 1;
        string destino;

        do
        {
            destino = Path.Combine(caminho, $"{nomeBase}_{contador}{extensao}");
            contador++;
        }
        while (File.Exists(destino));

        File.Move(caminhoAtual, destino);
    }

    // ---------- HELPERS ----------
    private string GetLogFilePath(int? idx)
    {
        string data = DateTime.Now.ToString("dd MM yyyy");

        if (idx.HasValue)
            return Path.Combine(_config.PathLog, $"[{idx}] {_config.FileName} {data}.txt");

        return Path.Combine(_config.PathLog, $"{_config.FileName} {data}.txt");
    }

    private void ExibirNoConsole(string mensagem)
    {
        if (_config.RunToService)
            return;

        if (!string.IsNullOrEmpty(_config.EPSName))
            Console.WriteLine($"[{_config.EPSName.Trim().PadRight(15)}] {mensagem}");
        else
            Console.WriteLine(mensagem);
    }
}
