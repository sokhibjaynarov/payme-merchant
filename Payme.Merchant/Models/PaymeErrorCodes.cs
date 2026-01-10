namespace Payme.Merchant.Models
{
  public static class PaymeErrorCodes
  {
    public const int TransportError = -32300;
    public const int AccessDenied = -32504;
    public const int MethodNotFound = -32601;
    public const int InvalidJson = -32700;
    public const int SystemError = -32400;

    public const int OrderNotFound = -31050;
    public const int WrongAmount = -31001;
    public const int UnableToComplete = -31008;
    public const int TransactionNotFound = -31003;

    public const int Pending = -31099;
    public const int TerminalState = -31098;
    public const int InProgress = -31088;
    public const int CantCancel = -31007;
  }
}
