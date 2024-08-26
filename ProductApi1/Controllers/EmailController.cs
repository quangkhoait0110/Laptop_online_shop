using System;
using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Mvc;

namespace ProductApi1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailController : ControllerBase
    {
        [HttpPost("send")]
        public IActionResult SendEmail()
        {
            try
            {
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("quangkhoadt0110@gmail.com", "cdou liyv omel jocg"),
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress("quangkhoadt0110@gmail.com"),
                    Subject = "Thông báo",
                    Body = "Hello",
                    IsBodyHtml = false,
                };

                mailMessage.To.Add("quangkhoadt1107@gmail.com");

                smtpClient.Send(mailMessage);

                return Ok("Email đã được gửi thành công.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception details: {ex}");
                return BadRequest($"Lỗi khi gửi email: {ex.Message}");
            }
        }
    }
}