using System;
using System.Threading.Tasks;

namespace BookKiosk.Kiosk.Services
{
    public interface IDialogService
    {
        Task<bool> ShowDialogAsync(string message, bool isTwoButtons);
        void RegisterDialogHandler(Func<string, bool, Task<bool>> handler);
    }
}
