namespace NotificationService.Domain.Interfaces;

public interface IAppLogger
{
    void Info(string mensagem, int? idx = null);
    void InfoArquivo(string mensagem, int? idx, string nomeArquivo = null);
    void Infoteste(string mensagem, int? idx = null);
    void ErroArquivo(string nomeArquivo, string mensagem, string eps, int? idx);
    void FinalizarProcessamento();
}
