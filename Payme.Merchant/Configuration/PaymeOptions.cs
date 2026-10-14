using System;

namespace Payme.Merchant.Configuration
{
    public class PaymeOptions
    {
        /// <summary>Always "Paycom" for the Merchant API.</summary>
        public string Login { get; set; } = "Paycom";

        /// <summary>The merchant key from the Payme cabinet (test key in the sandbox).</summary>
        public string Password { get; set; } = default!;

        /// <summary>A created transaction that is not performed within this time is cancelled (reason 4).</summary>
        public TimeSpan TransactionTimeout { get; set; } = TimeSpan.FromHours(12);
    }
}
