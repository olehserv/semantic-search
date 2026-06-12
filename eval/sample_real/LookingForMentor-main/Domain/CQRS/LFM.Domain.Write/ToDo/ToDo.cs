namespace LFM.Domain.Write.ToDo
{
    public class ToDo
    {
        public string JsonCommand { get; }

        public string OperationCode { get; }

        public ToDo(string jsonCommand, string operationCode)
        {
            JsonCommand = jsonCommand;
            OperationCode = operationCode;
        }
    }
}