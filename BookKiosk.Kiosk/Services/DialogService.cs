using System;
using System.Threading.Tasks;

namespace BookKiosk.Kiosk.Services
{
    public class DialogService : IDialogService
    {
        private Func<string, bool, Task<bool>>? _dialogHandler;

        public void RegisterDialogHandler(Func<string, bool, Task<bool>> handler)
        {
            _dialogHandler = handler;
        }

        public Task<bool> ShowDialogAsync(string message, bool isTwoButtons)
        {
            if (_dialogHandler != null)
            {
                return _dialogHandler(message, isTwoButtons);
            }
            return Task.FromResult(false);
        }
    }
}
