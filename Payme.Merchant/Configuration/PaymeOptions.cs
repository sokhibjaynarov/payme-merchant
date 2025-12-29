namespace Payme.Merchant.Configuration
{
    public class PaymeOptions
    {
        public string Login { get; set; } = "Paycom"; // Default for Payme
        public string Password { get; set; } = default!; // The Secret Key
        public string Key { get; set; } = default!; // Sometimes referred to as Key or Password in different integrations. We'll use Password as the Secret.

        // Timeout for operations if needed, but usually we just process immediately.
    }
}
