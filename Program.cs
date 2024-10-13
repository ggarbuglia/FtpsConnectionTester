using FluentFTP;
using FluentFTP.Client.BaseClient;
using Microsoft.Extensions.Configuration;
using System.Net.Mail;
using System.Text;
using NLog;

namespace FtpsConnectionTester
{
    internal class Program
    {
        private static IConfiguration? configuration;
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public static void Main()
        {
            configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            try
            {
                Execute(TestFtpConnection, 3);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Stopped program because of exception");
                SendEmailAlert(ex);
                throw;
            }
            finally 
            {
                LogManager.Shutdown();
            }
        }

        private static void Execute(Action action, int numberOfTries) 
        {
            var tries = 0;
            var exception = new Exception();

            while (tries <= numberOfTries) 
            {
                try
                {
                    action();
                    return;
                }
                catch (Exception ex)
                {
                    exception = ex;
                    tries++;
                    Thread.Sleep(30000);
                }
            }

            throw exception;
        }

        private static void TestFtpConnection() 
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            var host     = configuration.GetValue<string>("FTPS:Host");
            var port     = configuration.GetValue<int>("FTPS:Port");
            var username = configuration.GetValue<string>("FTPS:Username");
            var password = configuration.GetValue<string>("FTPS:Password");

            using var conn = new FtpClient(host, username, password, port);

            try
            {
                conn.Config.EncryptionMode = FtpEncryptionMode.Explicit;
                conn.ValidateCertificate += new FtpSslValidation(OnValidateCertificate);
                conn.Connect();

                if (!conn.IsConnected) 
                    throw new Exception("FTPS not connected.");

                if (!conn.IsAuthenticated) 
                    throw new Exception("FTPS not authenticated.");

                logger.Info("FTPS connected.");

                if (conn.FileExists("/7z2300-x64.msi"))
                {
                    logger.Info("FTPS file '7z2300-x64.msi' exists!");
                }
                else
                {
                    logger.Warn("FTPS file '7z2300-x64.msi' not found!");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw;
            }
            finally 
            {
                if (conn != null) 
                {
                    conn.Disconnect();
                    logger.Info("FTPS disconnected.");
                }
            }
        }

        private static void OnValidateCertificate(BaseFtpClient control, FtpSslValidationEventArgs e)
        {
            e.Accept = true;
        }

        private static void SendEmailAlert(Exception? ex) 
        {
            var body = $"<p>NO ME PUDE CONECTAR A FILEZILLA {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>";

            if (ex != null) 
            {
                body += "<p style=\"font-family: Consolas;font-size: 10pt;\">";
                body += $"{ex.Message}<br/>";
                body += $"{ex.Source}<br/>";
                body += $"{ex.StackTrace}<br/><br/>";

                if (ex.InnerException != null) 
                {
                    body += $"{ex.InnerException.Message}<br/>";
                    body += $"{ex.InnerException.Source}<br/>";
                    body += $"{ex.InnerException.StackTrace}<br/>";
                }
                body += "</p>";
            }

            if (configuration != null) 
            {
                var host      = configuration.GetValue<string>("SMTP:Host");
                var port      = configuration.GetValue<int>("SMTP:Port");
                var fmAddress = configuration.GetValue<string>("SMTP:FromAddress");
                var fmDisplay = configuration.GetValue<string>("SMTP:FromDisplayName");
                var toAddress = configuration.GetValue<string>("SMTP:ToAddress");
                var toDisplay = configuration.GetValue<string>("SMTP:ToDisplayName");
                var subject   = configuration.GetValue<string>("SMTP:Subject");

                var message = new MailMessage
                {
                    Priority     = MailPriority.High,
                    From         = new MailAddress(fmAddress, fmDisplay),
                    Subject      = subject,
                    Body         = body,
                    BodyEncoding = Encoding.UTF8,
                    IsBodyHtml   = true
                };

                message.To.Add(new MailAddress(toAddress, toDisplay));

                var smtpClient = new SmtpClient(host, port)
                {
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = true
                };

                smtpClient.Send(message);
                logger.Warn("Email alert sent.");
            }
        }
    }
}