namespace Payme.Merchant.Models
{
    public static class PaymeErrors
    {
        public static readonly PaymeErrorMessage OrderNotFound = new PaymeErrorMessage(
            "Номер телефона не найден",
            "Raqam ro'yhatda yo'q",
            "Phone number not found"
        );

        public static readonly PaymeErrorMessage WrongAmount = new PaymeErrorMessage(
            "Неверная сумма",
            "Noto'g'ri summa",
            "Incorrect amount"
        );

        public static readonly PaymeErrorMessage TransactionNotFound = new PaymeErrorMessage(
            "Транзакция не найдена",
            "Tranzaksiya topilmadi",
            "Transaction not found"
        );

        public static readonly PaymeErrorMessage TransactionInWaiting = new PaymeErrorMessage(
            "Транзакция в ожидании",
            "Tranzaksiya kutish holatida",
            "Transaction in waiting"
        );

        public static readonly PaymeErrorMessage UnableToCancel = new PaymeErrorMessage(
            "Невозможно отменить транзакцию",
            "Tranzaksiyani bekor qilib bo'lmaydi",
            "Unable to cancel transaction"
        );

        public static readonly PaymeErrorMessage UnableToComplete = new PaymeErrorMessage(
            "Невозможно выполнить операцию",
            "Operatsiyani amalga oshirib bo'lmaydi",
            "Unable to complete operation"
        );

        public static readonly PaymeErrorMessage Pending = new PaymeErrorMessage( // -31099
            "Временная ошибка. Попробуйте позже",
            "Vaqtinchalik xatolik. Keyinroq urinib ko'ring",
            "Temporary error. Try again later"
        );

        public static readonly PaymeErrorMessage TerminalState = new PaymeErrorMessage( // -31098
            "Транзакция уже завершена",
            "Tranzaksiya yakunlangan",
            "Transaction already completed"
        );
        
        public static readonly PaymeErrorMessage InProgress = new PaymeErrorMessage( // -31088
            "Транзакция в процессе",
            "Tranzaksiya jarayonda",
            "Transaction in progress"
        );
    }
}
