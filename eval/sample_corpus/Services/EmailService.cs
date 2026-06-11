using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace SampleShop.Services;

// Sends transactional emails to customers over SMTP. This is where outbound
// email (such as order confirmations) is composed and delivered.
public class EmailService
{
    private readonly SmtpClient _smtp;

    public EmailService()
    {
        _smtp = new SmtpClient("smtp.example.com", 587)
        {
            Credentials = new NetworkCredential("noreply@example.com", "secret"),
            EnableSsl = true,
        };
    }

    // Builds and sends an order-confirmation email to the customer.
    public async Task SendOrderConfirmationAsync(string customerEmail, int orderId)
    {
        var message = new MailMessage(
            from: "noreply@example.com",
            to: customerEmail,
            subject: $"Order #{orderId} confirmed",
            body: $"Thank you! Your order #{orderId} has been received.");

        await _smtp.SendMailAsync(message);
    }
}
