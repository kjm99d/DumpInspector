using System.Threading.Tasks;

namespace DumpInspector.Server.Services.Interfaces
{
    public interface IEmailSender
    {
        Task SendAsync(string to, string subject, string body);
    }
}
