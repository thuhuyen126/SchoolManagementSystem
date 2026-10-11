using System.Threading.Tasks;

namespace SchoolManagementSystem.Helpers
{
    public interface IAlertHelper
    {
        Task CreateAlertAsync(string message, string alertType, string userId);
    }
}
