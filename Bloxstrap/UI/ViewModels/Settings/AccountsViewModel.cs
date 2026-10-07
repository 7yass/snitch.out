using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class AccountRow
    {
        public long UserId { get; set; }
        public string Display { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public bool IsCurrent { get; set; }
    }

    public class AccountsViewModel : NotifyPropertyChangedViewModel
    {
        private string _statusText = "";

        public ObservableCollection<AccountRow> Accounts { get; } = new();

        public AccountRow? SelectedAccount { get; set; }

        public bool HasSelection => SelectedAccount is not null;

        public string StatusText
        {
            get => _statusText;
            private set
            {
                _statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public bool NeedsCookieAccess => !App.Cookies.Loaded;

        public ICommand SaveCurrentCommand => new RelayCommand(async () => await SaveCurrent());
        public ICommand SwitchCommand => new RelayCommand(Switch, () => HasSelection);
        public ICommand DeleteCommand => new RelayCommand(Delete, () => HasSelection);

        public AccountsViewModel()
        {
            Reload();
            App.Cookies.StateChanged += (_, _) => OnPropertyChanged(nameof(NeedsCookieAccess));
        }

        public void Reload()
        {
            Accounts.Clear();

            long? current = App.AccountVault.GetCurrentUserId();

            foreach (var account in App.AccountVault.Accounts.OrderBy(x => x.DisplayName))
            {
                bool isCurrent = current == account.UserId;

                Accounts.Add(new AccountRow
                {
                    UserId = account.UserId,
                    Display = String.IsNullOrEmpty(account.DisplayName) ? account.Username : $"{account.DisplayName} (@{account.Username})",
                    Subtitle = $"Added {account.AddedUtc.ToLocalTime():g}{(isCurrent ? " · current" : "")}",
                    IsCurrent = isCurrent
                });
            }

            OnPropertyChanged(nameof(Accounts));
        }

        private async Task SaveCurrent()
        {
            string? error = await App.AccountVault.SaveCurrentAsync();
            StatusText = error ?? "Current account saved.";
            Reload();
        }

        private void Switch()
        {
            if (SelectedAccount is null)
                return;

            string? error = App.AccountVault.SwitchTo(SelectedAccount.UserId);
            StatusText = error ?? $"Switched to {SelectedAccount.Display}. Launch Roblox to play on it.";
            Reload();
        }

        private void Delete()
        {
            if (SelectedAccount is null)
                return;

            var result = Frontend.ShowMessageBox(
                $"Forget {SelectedAccount.Display}? Its saved cookie will be deleted.",
                MessageBoxImage.Question,
                MessageBoxButton.YesNo
            );

            if (result != MessageBoxResult.Yes)
                return;

            App.AccountVault.Delete(SelectedAccount.UserId);
            SelectedAccount = null;
            OnPropertyChanged(nameof(HasSelection));
            StatusText = "Account forgotten.";
            Reload();
        }
    }
}
