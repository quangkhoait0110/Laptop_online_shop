using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace ProductApi1.Services
{
    public class EmailService
    {
        private readonly string _smtpServer = "smtp.gmail.com";
        private readonly int _port = 587;
        private readonly string _username = "quangkhoadt0110@gmail.com";
        private readonly string _password = "cdou liyv omel jocg";

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            using (var smtpClient = new SmtpClient(_smtpServer, _port))
            {
                smtpClient.Credentials = new NetworkCredential(_username, _password);
                smtpClient.EnableSsl = true;

                // Setting additional properties
                smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
                smtpClient.UseDefaultCredentials = false;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_username),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = false,
                };

                mailMessage.To.Add(toEmail);

                await smtpClient.SendMailAsync(mailMessage);
            }
        }
    }
}
