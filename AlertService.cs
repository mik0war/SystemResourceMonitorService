using System;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

public class AlertService(IConfiguration configuration)
{
    private readonly SmtpSettings _smtpSettings = 
        configuration.GetSection("SmtpSettings").Get<SmtpSettings>();

    public void SendEmail(string subject, string body)
    {
        try
        {
            using var smtpClient = _smtpSettings.GetClient();

            using var mailMessage = _smtpSettings.GetMailMessage(subject, body);

            smtpClient.Send(mailMessage);
            
            Console.WriteLine($"[ALERT] Email отправлен.  {subject}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Не удалось отправить email.  {ex.Message}");
        }
    }
}

public class SmtpSettings
{
    public string Host { get;  set; }
    public int Port { get; set; }
    public string Username { get; set; }
  
    public string Password { get; set; }

    public string From { get; set; }

    public string To { private get; set; }


    public MailMessage GetMailMessage(string subject, string body)
    {
        var mailMessage = new MailMessage
        {
            From = new MailAddress(From),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        mailMessage.To.Add(To);

        return mailMessage;
    }

    public SmtpClient GetClient()
    {
        var client = new SmtpClient(Host, Port)
        {
            Credentials = new NetworkCredential(Username, Password),
            EnableSsl = true
        };

        return client;
    }
}
