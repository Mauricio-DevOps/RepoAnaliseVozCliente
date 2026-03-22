namespace POCLeituradeVozCliente.Services;

public interface IExcelFeedbackReader
{
    List<string> ReadFirstColumnValues(string filePath);
}
